using System.Text;
using AmarV.Core.Analise;
using AmarV.Core.Entrada;
using AmarV.Core.Relatorios;
using AmarV.Core.Resultado;
using AmarV.Demonstracao;

// =====================================================================
// TESTES AUTOMATIZADOS DO MOTOR DE ANÁLISE
//
// Executar:  dotnet run --project tests/AmarV.Core.Testes
// Cada teste descreve uma regra esperada. Se algum falhar, o programa
// termina com código 1 e mostra qual regra foi quebrada.
// (Runner próprio, sem pacotes NuGet, para rodar em qualquer máquina.)
// =====================================================================

Console.OutputEncoding = Encoding.UTF8;
var regras = CarregadorRegras.DeArquivo(Path.Combine(AppContext.BaseDirectory, "dados", "regras_sp_2026.json"));
var motor = new MotorAnaliseVacinal(regras);
var prm = new ParametrosAnalise { DataReferencia = Cenarios.Hoje };
var cen = Cenarios.Todos().ToDictionary(c => c.Codigo);
ResultadoAnalise A(string codigo) => motor.Analisar(cen[codigo].Paciente, cen[codigo].Historico, prm);
ItemAnalise? Item(ResultadoAnalise r, string chave) =>
    r.ItensSUS.FirstOrDefault(i => i.ChaveVacina == chave);

int ok = 0, falhas = 0;
void Teste(string nome, Func<bool> condicao, Func<string>? detalhe = null)
{
    bool passou;
    string erro = "";
    try { passou = condicao(); }
    catch (Exception ex) { passou = false; erro = ex.GetType().Name + ": " + ex.Message; }
    if (passou) { ok++; Console.WriteLine($"  OK    {nome}"); }
    else
    {
        falhas++;
        Console.WriteLine($"  FALHA {nome} {erro}");
        if (detalhe != null) { try { Console.WriteLine("        " + detalhe()); } catch { } }
    }
}

Console.WriteLine("== Utilitários");
Teste("Idade em meses respeita o dia do mês", () =>
    Datas.IdadeEmMeses(new DateTime(2026, 8, 27), new DateTime(2026, 10, 26)) == 1 &&
    Datas.IdadeEmMeses(new DateTime(2026, 8, 26), new DateTime(2026, 10, 26)) == 2);
Teste("Intervalo '6 meses' usa calendário real (não 180 dias)", () =>
    Duracao.Interpretar("6 meses").SomarA(new DateTime(2026, 1, 31)) == new DateTime(2026, 7, 31));
Teste("Intervalos '28 dias', '4 semanas', '10 anos', 'Anual'", () =>
    Duracao.Interpretar("28 dias").Dias == 28 && Duracao.Interpretar("4 semanas").Dias == 28 &&
    Duracao.Interpretar("10 anos").Anos == 10 && Duracao.Interpretar("Anual").Anos == 1);
Teste("Nomes equivalentes viram a mesma vacina", () =>
    CatalogoVacinas.Chave("Tríplice Viral") == "scr" && CatalogoVacinas.Chave("Pneumocócica 20-valente") == CatalogoVacinas.Pneumo10 &&
    CatalogoVacinas.Chave("dTpa") == "dtpa" && CatalogoVacinas.Chave("dT") == "dt" && CatalogoVacinas.Chave("Meningocócica C") == "meningococica c" &&
    CatalogoVacinas.Chave("VSR (vírus sincicial respiratório A e B)") == "vsr");
Teste("Regras agrupadas em séries (rotina e recuperação separadas)", () =>
    motor.Series.Count(s => s.Chave == CatalogoVacinas.HepatiteB && s.GrupoEtario == "Criança") == 2 &&
    motor.Series.Count(s => s.Chave == CatalogoVacinas.VIP && s.GrupoEtario == "Criança") == 2);

Console.WriteLine("== C01 — bebê de 2 meses sem vacinas (bugs do motor antigo)");
var c01 = A("C01");
Teste("BCG aparece ATRASADA (deveria ter sido aplicada ao nascer)", () => Item(c01, "bcg")?.Prazo == SituacaoPrazo.Atrasada);
Teste("Penta, VIP, Pneumo 10 e Rotavírus indicadas hoje", () =>
    new[] { "pentavalente", "vip", CatalogoVacinas.Pneumo10, "rotavirus" }.All(k => Item(c01, k)?.Prazo == SituacaoPrazo.AplicarHoje));
Teste("Doses futuras aparecem com data (Meningo C aos 3 meses)", () =>
    Item(c01, "meningococica c") is { Prazo: SituacaoPrazo.Futura } m && m.DataMinima == cen["C01"].Paciente.DataNascimento.AddMonths(3));
Teste("Hepatite B ao nascer fica fora da faixa (segue com a penta)", () => Item(c01, "hepatite b")?.Prazo == SituacaoPrazo.ForaDaFaixaEtaria);
Teste("Plano começa hoje e tem as próximas doses com data", () =>
    c01.PlanoRecuperacao.Count > 3 && c01.PlanoRecuperacao[0].EhHoje &&
    c01.PlanoRecuperacao.Any(a => a.Doses.Any(d => d.ChaveVacina == "pentavalente" && d.NumeroDose == 3)));
Teste("Situação geral = EM ATRASO", () => c01.Situacao == SituacaoGeral.EmAtraso);
Teste("Limite de 2 IM por atendimento (≤ 24 meses) respeitado no plano", () =>
    c01.PlanoRecuperacao.All(a => a.Doses.Count(d => d.Via == "IM") <= 2));
Teste("Intervalo mínimo da penta respeitado (≥ 28 dias entre doses)", () =>
{
    var datas = c01.PlanoRecuperacao.Where(a => a.Doses.Any(d => d.ChaveVacina == "pentavalente")).Select(a => a.Data).ToList();
    return datas.Count == 3 && datas.Zip(datas.Skip(1)).All(p => (p.Second - p.First).TotalDays >= 28);
});

Console.WriteLine("== C02 / C04 — crianças com caderneta");
var c02 = A("C02");
Teste("Bebê com BCG e HepB da maternidade não está em atraso", () => c02.Situacao == SituacaoGeral.ComDosesParaAplicar);
var c04 = A("C04");
Teste("Criança com caderneta completa = REGULAR", () => c04.Situacao == SituacaoGeral.Regular,
    () => string.Join(", ", c04.ItensSUS.Where(i => i.Prazo is SituacaoPrazo.Atrasada or SituacaoPrazo.AplicarHoje).Select(i => i.Vacina)));
Teste("Reforços dos 4 anos aparecem como próximas doses", () =>
    new[] { "dtp", "varicela", "febre amarela" }.All(k => Item(c04, k)?.Prazo == SituacaoPrazo.Futura));

Console.WriteLine("== C03 — criança de 4 anos com caderneta incompleta");
var c03 = A("C03");
Teste("Doses dos 15 meses aparecem atrasadas", () =>
    new[] { "vip", "scr", "varicela", "hepatite a", "dtp" }.All(k => Item(c03, k)?.Prazo == SituacaoPrazo.Atrasada));
Teste("Pentavalente conta como hepatite B (esquema em dia)", () => Item(c03, "hepatite b")?.Prazo == SituacaoPrazo.EmDia);
Teste("Vivas injetáveis: no mesmo dia ou com ≥ 28 dias", () =>
{
    var vivas = c03.PlanoRecuperacao.Where(a => a.Doses.Any(d => CatalogoVacinas.EhVivaInjetavel(d.ChaveVacina))).Select(a => a.Data).ToList();
    return vivas.Zip(vivas.Skip(1)).All(p => (p.Second - p.First).TotalDays >= 28);
});
Teste("No máximo 2 SC e 4 IM por atendimento (> 24 meses)", () =>
    c03.PlanoRecuperacao.All(a => a.Doses.Count(d => d.Via == "SC") <= 2 && a.Doses.Count(d => d.Via == "IM") <= 4));

Console.WriteLine("== C05 — adulta lactante (cenário do teste de 09/10/2026)");
var c05 = A("C05");
var fa05 = Item(c05, "febre amarela");
Teste("Febre amarela NÃO fica liberada (restrição ADIAR)", () => fa05 is { Restricao: Restricao.Adiar } && !fa05.IndicadaHoje);
Teste("Febre amarela fica fora do plano de atendimentos", () =>
    c05.PlanoRecuperacao.SelectMany(a => a.Doses).All(d => d.ChaveVacina != "febre amarela"));
Teste("Adulto sem histórico: HepB, dT e SCR ATRASADAS", () =>
    new[] { "hepatite b", "dt", "scr" }.All(k => Item(c05, k)?.Prazo == SituacaoPrazo.Atrasada));
Teste("SCR de 30 a 59 anos: dose única", () => Item(c05, "scr")?.DosesNecessarias == 1);
Teste("Hepatite B: 3ª dose ≥ 16 semanas após a 1ª", () =>
{
    var d = c05.PlanoRecuperacao.Where(a => a.Doses.Any(x => x.ChaveVacina == "hepatite b")).Select(a => a.Data).ToList();
    return d.Count == 3 && (d[2] - d[0]).TotalDays >= 112;
});
Teste("dT: 3ª dose com intervalo (regra sem intervalo usa o da 2ª dose)", () =>
{
    var d = c05.PlanoRecuperacao.Where(a => a.Doses.Any(x => x.ChaveVacina == "dt")).Select(a => a.Data).ToList();
    return d.Count == 3 && (d[2] - d[1]).TotalDays >= 60;
});

Console.WriteLine("== C06 — gestante de 29 semanas");
var c06 = A("C06");
Teste("dTpa e VSR indicadas na gestação (≥ 20 e ≥ 28 semanas)", () =>
    Item(c06, "dtpa")?.Prazo == SituacaoPrazo.AplicarHoje && Item(c06, "vsr")?.Prazo == SituacaoPrazo.AplicarHoje);
Teste("Febre amarela contraindicada na gestação", () => Item(c06, "febre amarela")?.Restricao == Restricao.Contraindicada);
Teste("dT completa há 5 anos = em dia", () => Item(c06, "dt")?.Prazo == SituacaoPrazo.EmDia);
Teste("Gestante não fica 'em atraso' por vacina contraindicada", () => c06.Situacao == SituacaoGeral.ComDosesParaAplicar);
var gest20 = new PerfilPaciente { Id = 99, Nome = "Teste", DataNascimento = Cenarios.Hoje.AddYears(-25), Gestante = true, SemanasGestacao = 18 };
var r20 = motor.Analisar(gest20, new List<DoseAplicada>(), prm);
Teste("Gestante de 18 semanas: dTpa futura na 20ª semana", () =>
    Item(r20, "dtpa") is { Prazo: SituacaoPrazo.Futura } i && i.DataMinima == Cenarios.Hoje.AddDays(14));

Console.WriteLine("== C07 / C08 / C09 / C10");
var c07 = A("C07");
Teste("Idoso: reforço de dT vencido = ATRASADA", () => Item(c07, "dt") is { Prazo: SituacaoPrazo.Atrasada, ProximaEhReforco: true });
Teste("Idoso: influenza anual vencida = APLICAR HOJE", () => Item(c07, "influenza")?.Prazo == SituacaoPrazo.AplicarHoje);
Teste("Idoso: febre amarela exige avaliação", () => Item(c07, "febre amarela")?.Restricao == Restricao.Avaliar);
var c08 = A("C08");
Teste("Quimioterapia: dengue (viva) contraindicada", () => Item(c08, "dengue")?.Restricao == Restricao.Contraindicada);
Teste("Quimioterapia: HPV (inativada) indicada e atrasada", () => Item(c08, "hpv") is { Prazo: SituacaoPrazo.Atrasada, Restricao: Restricao.Nenhuma });
var c09 = A("C09");
Teste("Febre: alerta de bloqueio e nenhuma dose hoje", () =>
    c09.Alertas.Any(a => a.Nivel == NivelAlerta.Bloqueio) && c09.ItensSUS.All(i => !i.IndicadaHoje) &&
    c09.PlanoRecuperacao.All(a => a.Data > Cenarios.Hoje));
Teste("Pneumocócica 10 iniciada após 12 meses = dose única", () => Item(c09, CatalogoVacinas.Pneumo10)?.DosesNecessarias == 1);
var c10 = A("C10");
Teste("Profissional de saúde: 2ª dose de SCR atrasada", () => Item(c10, "scr") is { Prazo: SituacaoPrazo.Atrasada, ProximaDose: 2 });
Teste("Profissional de saúde: alerta de anti-HBs", () => c10.Alertas.Any(a => a.Mensagem.Contains("anti-HBs")));

Console.WriteLine("== Validações e relatórios");
var semNasc = motor.Analisar(new PerfilPaciente { Id = 50, Nome = "Sem data" }, new List<DoseAplicada>(), prm);
Teste("Sem data de nascimento = NÃO ANALISADO com aviso", () => semNasc.Situacao == SituacaoGeral.NaoAnalisado && semNasc.Avisos.Count > 0);
var futura = motor.Analisar(cen["C07"].Paciente,
    new List<DoseAplicada> { new() { NomeVacina = "Influenza", Data = Cenarios.Hoje.AddDays(5) } }, prm);
Teste("Dose com data futura é ignorada e gera aviso", () => futura.Avisos.Any(a => a.Contains("futura")));
Teste("Rede privada não altera a situação do SUS e não aparece 'atrasada'", () =>
    c04.ItensRedePrivada.Count > 0 && c04.ItensRedePrivada.All(i => i.Prazo != SituacaoPrazo.Atrasada) && c04.Situacao == SituacaoGeral.Regular);
var todos = cen.Keys.Select(k => new PacienteRelatorio { Analise = A(k) }).ToList();
Teste("Relatório RF21 (regulares) contém a criança em dia", () =>
    RelatorioConsolidado.Filtrar(todos, TipoRelatorio.PacientesRegulares).Any(p => p.Analise.PacienteId == 4));
Teste("Relatório RF20 (atrasados) usa a mesma análise da tela", () =>
    RelatorioConsolidado.Filtrar(todos, TipoRelatorio.PacientesEmAtraso).Count == todos.Count(p => p.Analise.Situacao == SituacaoGeral.EmAtraso));
Teste("Resumo para o banco preenche as colunas da análise", () =>
{
    var b = ResumoParaBanco.De(c05);
    return b.StatusVacinal == "EM ATRASO" && b.VacinasEmAtraso.Contains("Hepatite B") &&
           b.VacinasContraindicadas.Contains("Febre Amarela") && b.SugestaoRecuperacao.Length > 0;
});
Teste("HTML do relatório individual é gerado", () => FormatadorHtml.Gerar(c03).Contains("Esquema de recuperação"));
Teste("Resultado é reproduzível (mesma entrada → mesma saída)", () =>
    FormatadorTexto.Gerar(A("C03")) == FormatadorTexto.Gerar(A("C03")));

var cal = CalendarioResumo.Montar(regras);
Teste("Calendário montado a partir das regras (RF05)", () =>
    cal.Any(l => l.Vacina == "Pentavalente" && l.Esquema.Contains("3 dose(s)")) &&
    cal.Any(l => l.Calendario == "Rede Privada") && cal.Any(l => l.GrupoEtario == "Gestante"));

Console.WriteLine();
Console.WriteLine($"Resultado: {ok} teste(s) OK, {falhas} falha(s).");
return falhas == 0 ? 0 : 1;
