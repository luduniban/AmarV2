using AmarV.Core.Entrada;
using AmarV.Core.Resultado;

namespace AmarV.Core.Analise;

/// <summary>
/// Motor de análise do esquema vacinal (RF08 a RF14).
///
/// Passos:
///  1. calcula a idade na data de referência;
///  2. agrupa as regras em séries (esquemas) e escolhe as que valem para o paciente;
///  3. conta as doses do histórico (com equivalências, ex.: penta conta como hepatite B);
///  4. para cada vacina, define a próxima dose, a data mínima, a data recomendada e a situação
///     (em dia, aplicar hoje, atrasada, aguardando intervalo, futura, fora da faixa);
///  5. aplica contraindicações e precauções;
///  6. monta o plano de recuperação com datas (atendimentos);
///  7. define a situação geral do paciente (regular, com doses para aplicar, em atraso).
/// </summary>
public sealed class MotorAnaliseVacinal
{
    private readonly List<RegraCalendario> _regras;
    private readonly List<Serie> _series;
    private readonly Dictionary<int, RegraCalendario> _regraPorId;

    public MotorAnaliseVacinal(IEnumerable<RegraCalendario> regras)
    {
        _regras = regras.ToList();
        _series = MontadorEsquemas.MontarSeries(_regras);
        _regraPorId = _regras.Where(r => r.Id > 0).GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
    }

    public IReadOnlyList<Serie> Series => _series;

    public ResultadoAnalise Analisar(
        PerfilPaciente paciente,
        IEnumerable<DoseAplicada> historico,
        ParametrosAnalise? parametros = null)
    {
        var prm = parametros ?? new ParametrosAnalise();
        DateTime hoje = prm.DataReferencia.Date;

        var resultado = new ResultadoAnalise
        {
            PacienteId = paciente.Id,
            NomePaciente = string.IsNullOrWhiteSpace(paciente.Nome) ? $"Paciente {paciente.Id}" : paciente.Nome,
            DataNascimento = paciente.DataNascimento,
            DataAnalise = hoje
        };

        // ------------------------------------------------------------
        // 1. VALIDAÇÕES BÁSICAS
        // ------------------------------------------------------------
        if (paciente.DataNascimento == default || paciente.DataNascimento.Year < 1900 ||
            paciente.DataNascimento.Date > hoje)
        {
            resultado.Situacao = SituacaoGeral.NaoAnalisado;
            resultado.Avisos.Add("Data de nascimento ausente ou inválida: não é possível analisar o esquema vacinal.");
            return resultado;
        }

        int idade = Datas.IdadeEmMeses(paciente.DataNascimento, hoje);
        resultado.IdadeMeses = idade;
        resultado.IdadeTexto = Datas.IdadeTexto(paciente.DataNascimento, hoje);
        resultado.GrupoEtario = CatalogoVacinas.GrupoEtario(idade, paciente.Gestante);
        resultado.CondicoesConsideradas = DescreverCondicoes(paciente);

        if (_regras.Count == 0)
            resultado.Avisos.Add("Nenhuma regra vacinal cadastrada: carregue o calendário antes de analisar.");

        // ------------------------------------------------------------
        // 2. HISTÓRICO: doses por vacina (com equivalências)
        // ------------------------------------------------------------
        var dosesPorChave = ContarDoses(historico, paciente, hoje, resultado);

        // ------------------------------------------------------------
        // 3. ESQUEMAS APLICÁVEIS E AVALIAÇÃO DE CADA VACINA
        // ------------------------------------------------------------
        var esquemas = MontadorEsquemas.Selecionar(_series, paciente, idade, prm);

        foreach (var esquema in esquemas)
        {
            if (esquema.Calendario == Calendarios.RedePrivada && !prm.IncluirRedePrivada) continue;

            var doses = dosesPorChave.TryGetValue(esquema.Chave, out var lista) ? lista : new List<DoseInfo>();
            var item = AvaliarEsquema(esquema, doses, paciente, idade, prm, resultado);
            if (item == null) continue;

            // Rede privada: só interessa o que ainda pode ser feito
            if (item.EhRedePrivada && item.Prazo == SituacaoPrazo.ForaDaFaixaEtaria) continue;

            // Restrições clínicas (não se aplicam ao que já está em dia ou fora da faixa)
            if (item.Prazo is not (SituacaoPrazo.EmDia or SituacaoPrazo.ForaDaFaixaEtaria))
            {
                var (restricao, motivo) = RegrasSeguranca.Avaliar(esquema.Chave, paciente, idade);
                item.Restricao = restricao;
                item.MotivoRestricao = motivo;

                // Fica fora do plano: contraindicada, ou adiada por motivo que não seja só a febre de hoje
                // (com febre, o plano inteiro começa após a melhora).
                item.ForaDoPlano = restricao == Restricao.Contraindicada ||
                    (restricao == Restricao.Adiar &&
                     RegrasSeguranca.Avaliar(esquema.Chave, paciente, idade, ignorarFebre: true).restricao != Restricao.Nenhuma);
            }

            // Rede privada: vacinas complementares não ficam "atrasadas" — ficam disponíveis.
            if (item.EhRedePrivada && item.Prazo == SituacaoPrazo.Atrasada)
            {
                item.Prazo = SituacaoPrazo.AplicarHoje;
                item.Motivo = Texto.Normalizar(item.Indicacao).StartsWith("disponivel na rede privada")
                    ? item.Indicacao
                    : "Disponível na rede privada (complementar ao SUS). " + item.Indicacao;
            }

            (item.EhRedePrivada ? resultado.ItensRedePrivada : resultado.ItensSUS).Add(item);
        }

        resultado.ItensSUS = Ordenar(resultado.ItensSUS);
        resultado.ItensRedePrivada = Ordenar(resultado.ItensRedePrivada);

        // ------------------------------------------------------------
        // 4. ALERTAS, PLANO DE RECUPERAÇÃO E SITUAÇÃO GERAL
        // ------------------------------------------------------------
        resultado.Alertas.AddRange(RegrasSeguranca.AlertasGerais(paciente, idade, resultado.ItensSUS));
        resultado.PlanoRecuperacao = PlanejadorRecuperacao.Montar(resultado.ItensSUS, paciente, prm);

        if (resultado.Atrasadas.Any())
            resultado.Situacao = SituacaoGeral.EmAtraso;
        else if (resultado.ParaAplicarHoje.Any())
            resultado.Situacao = SituacaoGeral.ComDosesParaAplicar;
        else
            resultado.Situacao = SituacaoGeral.Regular;

        if (!historico.Any())
            resultado.Avisos.Add("Paciente sem histórico vacinal registrado. Se houver caderneta, registre as doses " +
                                 "antes de concluir a análise; sem comprovação, o esquema é considerado não iniciado.");

        return resultado;
    }

    // =================================================================
    // AVALIAÇÃO DE UM ESQUEMA
    // =================================================================

    private ItemAnalise? AvaliarEsquema(
        EsquemaPaciente e, List<DoseInfo> doses, PerfilPaciente p, int idade,
        ParametrosAnalise prm, ResultadoAnalise resultado)
    {
        DateTime hoje = prm.DataReferencia.Date;
        DateTime nasc = p.DataNascimento.Date;
        int tol = Math.Max(0, prm.ToleranciaAtrasoDias);

        var datadas = doses.Where(d => d.Data.HasValue).Select(d => d.Data!.Value.Date).OrderBy(d => d).ToList();
        DateTime? ultima = datadas.Count > 0 ? datadas[^1] : null;
        int N = doses.Count;
        int T = Math.Max(1, e.Total);

        var item = new ItemAnalise
        {
            Vacina = e.Nome,
            ChaveVacina = e.Chave,
            Calendario = e.Calendario,
            DosesRegistradas = N,
            UltimaAplicacao = ultima,
            PrimeiraAplicacao = datadas.Count > 0 ? datadas[0] : null,
            Periodicidade = e.Periodicidade
        };

        // ------------------------------------------------------------
        // Febre amarela: a partir de 5 anos, dose única; dose adicional
        // apenas se a 1ª dose foi aplicada antes dos 5 anos.
        // ------------------------------------------------------------
        if (e.Chave == CatalogoVacinas.FebreAmarela && !e.Expirado && idade >= 60)
        {
            bool algumaApos5 = datadas.Any(d => d >= nasc.AddYears(5));
            if (N == 0) T = 1;
            else if (algumaApos5 || N >= 2) T = N;
            else T = 2;
            item.Observacao = "A partir de 5 anos: dose única. Dose adicional somente se a 1ª dose foi aplicada antes dos 5 anos.";
        }

        // ------------------------------------------------------------
        // Pneumocócica 10-valente: de 12 meses a 4 anos sem vacinação, dose única;
        // quem iniciou o esquema após 12 meses não precisa de reforço.
        // ------------------------------------------------------------
        if (e.Chave == CatalogoVacinas.Pneumo10 && !e.Expirado && idade >= 12)
        {
            bool algumaApos12m = datadas.Any(d => d >= nasc.AddMonths(12));
            if (N == 0) T = 1;
            else if (algumaApos12m) T = N;
            item.Observacao = "De 12 meses a 4 anos sem vacinação: dose única. Quem iniciou após 12 meses não precisa de reforço.";
        }

        item.DosesNecessarias = T;

        // ------------------------------------------------------------
        // Dose por gestação (dTpa, VSR, COVID-19 na gestante)
        // ------------------------------------------------------------
        if (e.PorGestacao)
        {
            var regra = e.PrimeiraRegra;
            PreencherRegra(item, regra);
            int semanas = Math.Max(0, p.SemanasGestacao);
            DateTime inicio = semanas > 0 ? hoje.AddDays(-7 * semanas) : hoje.AddMonths(-9);
            var nestaGestacao = datadas.Where(d => d >= inicio).ToList();
            item.DosesRegistradas = nestaGestacao.Count;
            item.DosesNecessarias = 1;
            int semMin = e.SemanaGestacionalMinima;

            if (nestaGestacao.Count > 0)
            {
                item.Prazo = SituacaoPrazo.EmDia;
                item.Motivo = $"Aplicada nesta gestação em {nestaGestacao[^1]:dd/MM/yyyy}.";
                return item;
            }

            item.ProximaDose = 1;
            item.RegraProximaDoseId = regra.Id > 0 ? regra.Id : null;

            if (semMin > 0 && semanas > 0 && semanas < semMin)
            {
                item.Prazo = SituacaoPrazo.Futura;
                item.DataMinima = item.DataRecomendada = inicio.AddDays(7 * semMin);
                item.Motivo = $"Indicada em cada gestação a partir da {semMin}ª semana " +
                              $"(prevista para {item.DataMinima:dd/MM/yyyy}; hoje: {semanas} semanas).";
            }
            else
            {
                item.Prazo = SituacaoPrazo.AplicarHoje;
                item.DataMinima = item.DataRecomendada = hoje;
                item.Motivo = semMin > 0
                    ? (semanas > 0
                        ? $"Indicada em cada gestação a partir da {semMin}ª semana; gestante com {semanas} semanas."
                        : $"Indicada em cada gestação a partir da {semMin}ª semana. Confirmar a idade gestacional.")
                    : "Indicada em cada gestação; não há dose registrada nesta gestação.";
            }
            item.Orientacao = "Aplicar 1 dose nesta gestação.";
            item.DosesRestantes.Add(Projetar(1, item.DataMinima!.Value, regra, nasc));
            return item;
        }

        // ------------------------------------------------------------
        // Dose do puerpério (dTpa para puérpera que não recebeu na gestação)
        // ------------------------------------------------------------
        if (e.PorPuerperio)
        {
            var regra = e.PrimeiraRegra;
            PreencherRegra(item, regra);
            var recente = datadas.Where(d => d >= hoje.AddMonths(-11)).ToList();
            item.DosesNecessarias = 1;
            item.DosesRegistradas = recente.Count;
            if (recente.Count > 0)
            {
                item.Prazo = SituacaoPrazo.EmDia;
                item.Motivo = $"Dose recente registrada em {recente[^1]:dd/MM/yyyy} (gestação/puerpério atual).";
                return item;
            }
            item.ProximaDose = 1;
            item.RegraProximaDoseId = regra.Id > 0 ? regra.Id : null;
            item.Prazo = SituacaoPrazo.AplicarHoje;
            item.DataMinima = item.DataRecomendada = hoje;
            item.Motivo = "Puérpera sem dose registrada na gestação: indicada no puerpério.";
            item.Orientacao = "Aplicar 1 dose no puerpério.";
            item.DosesRestantes.Add(Projetar(1, hoje, regra, nasc));
            return item;
        }

        var periodo = Duracao.Interpretar(e.Periodicidade);

        // ------------------------------------------------------------
        // Dose única periódica (influenza anual, COVID semestral, dTpa 10 anos)
        // ------------------------------------------------------------
        if (!periodo.Vazia && T == 1)
        {
            var regra = e.PrimeiraRegra;
            PreencherRegra(item, regra);
            item.RegraProximaDoseId = regra.Id > 0 ? regra.Id : null;
            DateTime dataIdadeMin = Datas.DataNaIdade(nasc, regra.IdadeMinimaMeses);
            string rotulo = DescreverPeriodo(e.Periodicidade, periodo);
            item.RotuloProximaDose = periodo.Anos >= 5 ? (N > 0 ? "reforço" : "1ª dose") : $"dose {rotulo}";

            if (ultima == null)
            {
                item.ProximaDose = N + 1;
                if (hoje < dataIdadeMin)
                {
                    item.Prazo = SituacaoPrazo.Futura;
                    item.DataMinima = item.DataRecomendada = dataIdadeMin;
                    item.Motivo = $"Dose {rotulo} a partir de {dataIdadeMin:dd/MM/yyyy}.";
                }
                else
                {
                    item.Prazo = SituacaoPrazo.AplicarHoje;
                    item.DataMinima = item.DataRecomendada = hoje;
                    item.Motivo = N > 0
                        ? $"Há dose registrada sem data; não é possível saber se ainda vale. Dose {rotulo} indicada."
                        : $"Nenhuma dose registrada. Dose {rotulo} indicada.";
                }
            }
            else
            {
                DateTime proxima = periodo.SomarA(ultima.Value);
                if (proxima <= hoje)
                {
                    item.ProximaDose = N + 1;
                    item.ProximaEhReforco = N > 0 && periodo.Anos >= 5;
                    item.DataMinima = item.DataRecomendada = proxima;
                    // Reforços longos (5/10 anos) vencidos contam como atraso; campanhas anuais, não.
                    item.Prazo = periodo.Anos >= 5 && proxima.AddDays(tol) < hoje
                        ? SituacaoPrazo.Atrasada
                        : SituacaoPrazo.AplicarHoje;
                    item.Motivo = $"Última dose em {ultima:dd/MM/yyyy}; nova dose ({rotulo}) indicada desde {proxima:dd/MM/yyyy}.";
                }
                else
                {
                    item.Prazo = SituacaoPrazo.EmDia;
                    item.DataRecomendada = proxima;
                    item.Motivo = $"Última dose em {ultima:dd/MM/yyyy}; próxima ({rotulo}) a partir de {proxima:dd/MM/yyyy}.";
                    return item;
                }
            }

            item.Orientacao = $"Aplicar dose {rotulo}.";
            item.DosesRestantes.Add(Projetar(item.ProximaDose ?? 1, Datas.Max(hoje, item.DataMinima ?? hoje), regra, nasc));
            return item;
        }

        // ------------------------------------------------------------
        // Esquema completo (com ou sem reforço periódico, ex.: dT a cada 10 anos)
        // ------------------------------------------------------------
        if (N >= T)
        {
            var regra = e.UltimaRegra;
            PreencherRegra(item, regra);

            if (!periodo.Vazia && ultima != null)
            {
                DateTime proxima = periodo.SomarA(ultima.Value);
                if (proxima <= hoje)
                {
                    item.ProximaDose = N + 1;
                    item.ProximaEhReforco = true;
                    item.RotuloProximaDose = "reforço";
                    item.RegraProximaDoseId = regra.Id > 0 ? regra.Id : null;
                    item.DataMinima = item.DataRecomendada = proxima;
                    item.Prazo = proxima.AddDays(tol) < hoje ? SituacaoPrazo.Atrasada : SituacaoPrazo.AplicarHoje;
                    item.Motivo = $"Esquema completo ({N} doses). Reforço a cada {periodo} vencido em {proxima:dd/MM/yyyy} " +
                                  $"(última dose em {ultima:dd/MM/yyyy}).";
                    item.Orientacao = "Aplicar dose de reforço.";
                    item.DosesRestantes.Add(Projetar(N + 1, hoje, regra, nasc));
                    return item;
                }

                item.Prazo = SituacaoPrazo.EmDia;
                item.DataRecomendada = proxima;
                item.Motivo = $"Esquema completo ({N} doses). Próximo reforço em {proxima:dd/MM/yyyy}.";
                return item;
            }

            item.Prazo = SituacaoPrazo.EmDia;
            item.Motivo = !periodo.Vazia
                ? $"Esquema completo ({N} doses). Data da última dose não informada: não é possível calcular o reforço."
                : N > T
                    ? $"Esquema completo ({N} doses registradas)."
                    : $"Esquema completo ({N} de {T} dose(s)).";
            if (EquivalenciaUsada(e.Chave, doses))
                item.Motivo += " Inclui doses de vacinas combinadas (ex.: pentavalente).";
            return item;
        }

        // ------------------------------------------------------------
        // Esquema incompleto: próxima dose
        // ------------------------------------------------------------
        int n = N + 1;
        RegraCalendario regraN = e.Dose(n) ?? e.UltimaRegra;
        bool regraFaltando = e.Dose(n) == null;

        if (e.Expirado || idade > regraN.IdadeMaximaEfetiva)
        {
            // Esquema já iniciado: pode seguir para uma dose posterior que ainda caiba na idade
            // (ex.: pneumocócica — perdeu a 2ª dose antes de 12 m, segue para o reforço).
            bool pulou = false;
            if (!e.Expirado && N >= 1)
            {
                for (int m = n + 1; m <= T; m++)
                {
                    var r = e.Dose(m);
                    if (r != null && idade <= r.IdadeMaximaEfetiva)
                    {
                        item.Observacao = $"A {n}ª dose só pode ser aplicada até {Datas.FormatarIdadeMeses(regraN.IdadeMaximaEfetiva)}; " +
                                          $"o esquema segue para a {m}ª dose.";
                        n = m; regraN = r; pulou = true;
                        break;
                    }
                }
            }

            if (!pulou)
            {
                PreencherRegra(item, regraN);
                item.Prazo = SituacaoPrazo.ForaDaFaixaEtaria;
                item.ProximaDose = n;
                item.DataLimite = Datas.UltimoDiaNaIdade(nasc, regraN.IdadeMaximaEfetiva);
                item.Motivo = (N == 0 ? "Esquema não iniciado. " : $"Esquema incompleto ({N} de {T}). ") +
                              $"A {n}ª dose só pode ser aplicada até {Datas.FormatarIdadeMeses(regraN.IdadeMaximaEfetiva)} " +
                              $"(prazo encerrado em {item.DataLimite:dd/MM/yyyy})." +
                              (string.IsNullOrWhiteSpace(item.Indicacao) ? "" : $" Indicação: {item.Indicacao}");
                item.Orientacao = "Não recuperar por este esquema. Verificar se outra vacina substitui (ver indicação).";
                return item;
            }
        }

        PreencherRegra(item, regraN);
        item.ProximaDose = n;
        item.RegraProximaDoseId = regraN.Id > 0 ? regraN.Id : null;

        string textoIntervalo = IntervaloDaDose(e, regraN, item);
        var intervalo = Duracao.Interpretar(textoIntervalo);
        DateTime dataIdadeMinima = Datas.DataNaIdade(nasc, regraN.IdadeMinimaMeses);
        DateTime dataMinima = dataIdadeMinima;
        if (N >= 1 && ultima != null && !intervalo.Vazia)
            dataMinima = Datas.Max(dataMinima, intervalo.SomarA(ultima.Value));
        int desdePrimeira = CatalogoVacinas.DiasMinimosDesdeAPrimeiraDose(e.Chave, n);
        if (desdePrimeira > 0 && datadas.Count > 0 && N == datadas.Count)
            dataMinima = Datas.Max(dataMinima, datadas[0].AddDays(desdePrimeira));
        if (N >= 1 && ultima == null && !intervalo.Vazia)
            resultado.Avisos.Add($"{e.Nome}: doses anteriores sem data — o intervalo mínimo para a {n}ª dose não foi conferido.");

        DateTime dataRecomendada = Datas.Max(Datas.DataNaIdade(nasc, regraN.IdadeRecomendadaMeses), dataMinima);
        item.DataMinima = dataMinima;
        item.DataRecomendada = dataRecomendada;
        if (regraN.IdadeMaximaEfetiva < 9999)
            item.DataLimite = Datas.UltimoDiaNaIdade(nasc, regraN.IdadeMaximaEfetiva);

        string ordinal = $"{n}ª dose";
        if (hoje < dataMinima)
        {
            if (hoje < dataIdadeMinima)
            {
                item.Prazo = SituacaoPrazo.Futura;
                item.Motivo = $"{ordinal} a partir de {dataMinima:dd/MM/yyyy} " +
                              $"(idade mínima: {Datas.FormatarIdadeMeses(regraN.IdadeMinimaMeses)}).";
            }
            else
            {
                item.Prazo = SituacaoPrazo.AguardandoIntervalo;
                item.Motivo = $"{ordinal} a partir de {dataMinima:dd/MM/yyyy} " +
                              $"(intervalo mínimo de {intervalo} após a dose de {ultima:dd/MM/yyyy}).";
            }
        }
        else if (hoje > dataRecomendada.AddDays(tol))
        {
            item.Prazo = SituacaoPrazo.Atrasada;
            item.Motivo = (N == 0 ? "Nenhuma dose registrada. " : "") +
                          $"{ordinal} recomendada desde {dataRecomendada:dd/MM/yyyy} " +
                          $"({DescreverAtraso(dataRecomendada, hoje)} de atraso).";
        }
        else
        {
            item.Prazo = SituacaoPrazo.AplicarHoje;
            item.Motivo = $"{ordinal} indicada agora (recomendada a partir de {dataRecomendada:dd/MM/yyyy}).";
        }

        if (item.DataLimite.HasValue && item.Prazo != SituacaoPrazo.Futura)
            item.Motivo += $" Pode ser aplicada até {item.DataLimite:dd/MM/yyyy}.";

        if (regraFaltando)
            item.Observacao = (item.Observacao + $" Regra da {n}ª dose não cadastrada: conferir o esquema no calendário.").Trim();

        item.Orientacao = N == 0
            ? (T > 1 ? $"Iniciar esquema de {T} doses." : "Aplicar dose única.")
            : $"Aplicar a {n}ª dose para completar o esquema ({N} de {T} aplicadas).";

        // Projeção das doses que faltam (sem considerar limites por atendimento)
        DateTime anterior = Datas.Max(hoje, dataMinima);
        DateTime primeiraDose = datadas.Count > 0 ? datadas[0] : anterior;
        var dn = Projetar(n, anterior, regraN, nasc, textoIntervalo);
        dn.DiasMinimosDesdePrimeira = desdePrimeira;
        item.DosesRestantes.Add(dn);
        for (int k = n + 1; k <= T; k++)
        {
            var rk = e.Dose(k) ?? regraN;
            string tk = IntervaloDaDose(e, rk, item);
            var ik = Duracao.Interpretar(tk);
            DateTime dk = Datas.Max(Datas.DataNaIdade(nasc, rk.IdadeMinimaMeses), ik.Vazia ? anterior : ik.SomarA(anterior));
            int dpk = CatalogoVacinas.DiasMinimosDesdeAPrimeiraDose(e.Chave, k);
            if (dpk > 0) dk = Datas.Max(dk, primeiraDose.AddDays(dpk));
            if (dk > Datas.UltimoDiaNaIdade(nasc, rk.IdadeMaximaEfetiva)) break;
            var pk = Projetar(k, dk, rk, nasc, tk);
            pk.DiasMinimosDesdePrimeira = dpk;
            item.DosesRestantes.Add(pk);
            anterior = dk;
        }

        return item;
    }

    // =================================================================
    // HISTÓRICO
    // =================================================================

    internal sealed record DoseInfo(DateTime? Data, string ChaveOriginal);

    private Dictionary<string, List<DoseInfo>> ContarDoses(
        IEnumerable<DoseAplicada> historico, PerfilPaciente p, DateTime hoje, ResultadoAnalise resultado)
    {
        var porChave = new Dictionary<string, List<DoseInfo>>();
        var resumo = new Dictionary<string, ResumoHistorico>();

        foreach (var h in historico)
        {
            string nome = h.NomeVacina;
            if (string.IsNullOrWhiteSpace(nome) && _regraPorId.TryGetValue(h.RegraVacinalId, out var regra))
                nome = regra.NomeVacina;
            if (string.IsNullOrWhiteSpace(nome)) continue;

            int qtd = h.Quantidade;
            if (qtd <= 0) continue;

            DateTime? data = h.Data.HasValue && h.Data.Value != default && h.Data.Value.Year > 1900
                ? h.Data.Value.Date : null;

            if (data.HasValue && data.Value > hoje)
            {
                resultado.Avisos.Add($"{nome}: dose com data futura ({data:dd/MM/yyyy}) ignorada na análise.");
                continue;
            }
            if (data.HasValue && data.Value < p.DataNascimento.Date)
                resultado.Avisos.Add($"{nome}: dose com data anterior ao nascimento ({data:dd/MM/yyyy}); conferir o registro.");

            string chave = CatalogoVacinas.Chave(nome);

            // Expande registros antigos com várias doses na mesma linha:
            // só a última dose tem data conhecida.
            var expandidas = new List<DoseInfo>();
            for (int i = 0; i < qtd; i++)
                expandidas.Add(new DoseInfo(i == qtd - 1 ? data : null, chave));

            foreach (var destino in CatalogoVacinas.ContaComo(chave))
            {
                if (!porChave.TryGetValue(destino, out var lista))
                    porChave[destino] = lista = new List<DoseInfo>();
                lista.AddRange(expandidas);
            }

            string chaveResumo = Texto.Normalizar(nome);
            if (!resumo.TryGetValue(chaveResumo, out var r))
                resumo[chaveResumo] = r = new ResumoHistorico { Vacina = nome.Trim() };
            r.Doses += qtd;
            if (data.HasValue)
            {
                if (r.PrimeiraAplicacao == null || data < r.PrimeiraAplicacao) r.PrimeiraAplicacao = data;
                if (r.UltimaAplicacao == null || data > r.UltimaAplicacao) r.UltimaAplicacao = data;
            }
        }

        resultado.Historico = resumo.Values.OrderBy(r => r.Vacina).ToList();
        return porChave;
    }

    private static bool EquivalenciaUsada(string chave, List<DoseInfo> doses) =>
        doses.Any(d => d.ChaveOriginal != chave);

    // =================================================================
    // AUXILIARES
    // =================================================================

    /// <summary>
    /// Intervalo da regra; se a dose (2ª em diante) estiver sem intervalo cadastrado,
    /// usa o da dose anterior e registra a inconsistência (ex.: dT adulto/idoso, 3ª dose).
    /// </summary>
    private static string IntervaloDaDose(EsquemaPaciente e, RegraCalendario r, ItemAnalise item)
    {
        if (!string.IsNullOrWhiteSpace(r.IntervaloEntreDoses) || r.NumeroDose <= 1) return r.IntervaloEntreDoses ?? string.Empty;
        for (int d = r.NumeroDose - 1; d >= 2; d--)
        {
            var anterior = e.Dose(d);
            if (anterior != null && !string.IsNullOrWhiteSpace(anterior.IntervaloEntreDoses))
            {
                string aviso = $"Regra da {r.NumeroDose}ª dose sem intervalo cadastrado (Id {r.Id}); usado o intervalo da {d}ª dose ({anterior.IntervaloEntreDoses}).";
                if (!item.Observacao.Contains(aviso)) item.Observacao = (item.Observacao + " " + aviso).Trim();
                return anterior.IntervaloEntreDoses;
            }
        }
        return string.Empty;
    }

    private static DoseProjetada Projetar(int numero, DateTime data, RegraCalendario r, DateTime nasc, string? intervalo = null) => new()
    {
        NumeroDose = numero,
        DataPrevista = data,
        RegraId = r.Id > 0 ? r.Id : null,
        Via = CatalogoVacinas.Via(r.ViaAdministracao),
        DataIdadeMinima = Datas.DataNaIdade(nasc, r.IdadeMinimaMeses),
        DataLimite = r.IdadeMaximaEfetiva < 9999 ? Datas.UltimoDiaNaIdade(nasc, r.IdadeMaximaEfetiva) : null,
        Intervalo = intervalo ?? r.IntervaloEntreDoses ?? string.Empty
    };

    private static void PreencherRegra(ItemAnalise item, RegraCalendario r)
    {
        item.Via = CatalogoVacinas.Via(r.ViaAdministracao);
        item.Fonte = r.FonteCalendario ?? string.Empty;
        item.Indicacao = r.Indicacao ?? string.Empty;
        item.Contraindicacao = r.Contraindicacao ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(r.Observacao))
            item.Observacao = string.IsNullOrWhiteSpace(item.Observacao) ? r.Observacao : $"{item.Observacao} {r.Observacao}";
    }

    private static string DescreverPeriodo(string texto, Duracao d)
    {
        string t = Texto.Normalizar(texto);
        if (t.Contains("anual")) return "anual";
        if (t.Contains("semestral")) return "semestral";
        return $"a cada {d}";
    }

    private static string DescreverAtraso(DateTime desde, DateTime hoje)
    {
        int meses = Datas.IdadeEmMeses(desde, hoje);
        if (meses >= 24) return $"{meses / 12} anos";
        if (meses >= 12) return meses == 12 ? "1 ano" : $"1 ano e {meses - 12} {(meses - 12 == 1 ? "mês" : "meses")}";
        if (meses >= 1) return meses == 1 ? "1 mês" : $"{meses} meses";
        int dias = (hoje - desde).Days;
        return dias == 1 ? "1 dia" : $"{dias} dias";
    }

    private static List<ItemAnalise> Ordenar(List<ItemAnalise> itens)
    {
        static int Peso(ItemAnalise i) => i.Restricao == Restricao.Contraindicada ? 6 : i.Prazo switch
        {
            SituacaoPrazo.Atrasada => 0,
            SituacaoPrazo.AplicarHoje => 1,
            SituacaoPrazo.AguardandoIntervalo => 2,
            SituacaoPrazo.Futura => 3,
            SituacaoPrazo.EmDia => 4,
            _ => 5
        };
        return itens.OrderBy(Peso).ThenBy(i => i.DataMinima ?? DateTime.MaxValue).ThenBy(i => i.Vacina).ToList();
    }

    private static List<string> DescreverCondicoes(PerfilPaciente p)
    {
        var c = new List<string>();
        if (p.Gestante) c.Add(p.SemanasGestacao > 0 ? $"Gestante ({p.SemanasGestacao} semanas)" : "Gestante");
        if (p.Puerpera) c.Add("Puérpera");
        if (p.Lactante) c.Add("Lactante");
        if (p.CriancaAmamentadaMenor6Meses) c.Add("Amamenta criança < 6 meses");
        if (p.ProfissionalSaude) c.Add("Profissional de saúde");
        if (p.Indigena) c.Add("Indígena");
        if (p.AcamadoOuInstitucionalizado) c.Add("Acamado/institucionalizado");
        if (p.Imunossuprimido) c.Add("Imunossuprimido");
        if (p.HIV) c.Add("HIV");
        if (p.Quimioterapia) c.Add("Quimioterapia");
        if (p.Transplantado) c.Add("Transplantado");
        if (p.UsoCorticoideOuImunobiologico) c.Add("Corticoide/imunobiológico");
        if (p.AlergiaGrave) c.Add("Alergia grave");
        if (p.FebreOuDoencaAguda) c.Add("Febre/doença aguda");
        if (!string.IsNullOrWhiteSpace(p.CondicaoClinica)) c.Add(p.CondicaoClinica.Trim());
        return c;
    }
}
