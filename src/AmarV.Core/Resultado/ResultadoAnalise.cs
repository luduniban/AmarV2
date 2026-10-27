namespace AmarV.Core.Resultado;

// =====================================================================
// RESULTADO ESTRUTURADO DA ANÁLISE
//
// Em vez de devolver só um texto, o motor devolve objetos. A tela usa
// esses objetos para pintar cartões, o relatório usa para contar
// pacientes atrasados/regulares, e o banco grava cada lista em sua
// própria coluna (VacinasEmAtraso, VacinasPendentes, etc.).
// =====================================================================

/// <summary>Situação de prazo da próxima dose de uma vacina.</summary>
public enum SituacaoPrazo
{
    /// <summary>Esquema completo (ou dose periódica dentro da validade).</summary>
    EmDia,
    /// <summary>A dose já pode ser aplicada e está dentro do prazo recomendado (com tolerância).</summary>
    AplicarHoje,
    /// <summary>A dose já deveria ter sido aplicada (prazo recomendado + tolerância vencido).</summary>
    Atrasada,
    /// <summary>Esquema iniciado; a próxima dose depende do intervalo mínimo.</summary>
    AguardandoIntervalo,
    /// <summary>O paciente ainda não atingiu a idade mínima.</summary>
    Futura,
    /// <summary>A idade máxima para iniciar/continuar o esquema já passou (não recuperar).</summary>
    ForaDaFaixaEtaria
}

/// <summary>Restrição clínica que se sobrepõe ao prazo.</summary>
public enum Restricao
{
    Nenhuma,
    /// <summary>Precaução: aplicar somente após avaliação do profissional/serviço.</summary>
    Avaliar,
    /// <summary>Adiar (ex.: febre, lactante de bebê &lt; 6 meses para febre amarela).</summary>
    Adiar,
    /// <summary>Não aplicar (ex.: vacina viva em imunossuprimido ou gestante).</summary>
    Contraindicada
}

/// <summary>Situação geral do paciente (usada nos relatórios RF20 e RF21).</summary>
public enum SituacaoGeral
{
    /// <summary>Nenhuma dose do SUS atrasada nem indicada para hoje.</summary>
    Regular,
    /// <summary>Há doses indicadas para hoje, mas nenhuma atrasada.</summary>
    ComDosesParaAplicar,
    /// <summary>Há ao menos uma dose do SUS atrasada.</summary>
    EmAtraso,
    /// <summary>Não foi possível analisar (ex.: sem data de nascimento).</summary>
    NaoAnalisado
}

public enum NivelAlerta { Informacao, Atencao, Bloqueio }

/// <summary>Uma vacina analisada (uma linha/cartão na tela).</summary>
public sealed class ItemAnalise
{
    public string Vacina { get; set; } = string.Empty;
    public string ChaveVacina { get; set; } = string.Empty;
    public string Calendario { get; set; } = "SUS";
    public bool EhRedePrivada => Calendario == Calendarios.RedePrivada;

    public int DosesRegistradas { get; set; }
    public int DosesNecessarias { get; set; }

    /// <summary>Número da próxima dose (null quando o esquema está completo).</summary>
    public int? ProximaDose { get; set; }
    public bool ProximaEhReforco { get; set; }

    /// <summary>Como chamar a próxima dose: "2ª dose", "reforço", "dose anual"...</summary>
    public string RotuloProximaDose
    {
        get => string.IsNullOrEmpty(_rotulo) ? (ProximaDose is int n ? $"{n}ª dose" : string.Empty) : _rotulo;
        set => _rotulo = value;
    }
    private string _rotulo = string.Empty;

    /// <summary>Não entra no plano de recuperação (contraindicada ou adiada por condição que não é só febre).</summary>
    public bool ForaDoPlano { get; set; }

    /// <summary>Data a partir da qual a próxima dose pode ser aplicada.</summary>
    public DateTime? DataMinima { get; set; }

    /// <summary>Data recomendada para a próxima dose (ou do próximo reforço, quando em dia).</summary>
    public DateTime? DataRecomendada { get; set; }

    /// <summary>Último dia em que a próxima dose ainda pode ser aplicada (idade máxima).</summary>
    public DateTime? DataLimite { get; set; }

    public DateTime? UltimaAplicacao { get; set; }
    public DateTime? PrimeiraAplicacao { get; set; }

    public SituacaoPrazo Prazo { get; set; }
    public Restricao Restricao { get; set; }

    /// <summary>Explicação curta do prazo (por que está atrasada, quando pode aplicar...).</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Explicação da restrição clínica, quando houver.</summary>
    public string MotivoRestricao { get; set; } = string.Empty;

    /// <summary>Orientação de recuperação (o que fazer).</summary>
    public string Orientacao { get; set; } = string.Empty;

    public string Via { get; set; } = string.Empty;
    public string Periodicidade { get; set; } = string.Empty;
    public string Fonte { get; set; } = string.Empty;
    public string Indicacao { get; set; } = string.Empty;
    public string Contraindicacao { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;

    /// <summary>Id da regra da próxima dose (usado para registrar a aplicação direto da análise).</summary>
    public int? RegraProximaDoseId { get; set; }

    /// <summary>Doses que ainda faltam, com a data prevista de cada uma (projeção sem limites por atendimento).</summary>
    public List<DoseProjetada> DosesRestantes { get; set; } = new();

    /// <summary>Verdadeiro quando a dose pode ser aplicada hoje (prazo vencido ou no prazo e sem restrição impeditiva).</summary>
    public bool IndicadaHoje =>
        (Prazo == SituacaoPrazo.Atrasada || Prazo == SituacaoPrazo.AplicarHoje) &&
        Restricao == Restricao.Nenhuma;

    /// <summary>Texto curto da situação, para etiquetas na tela.</summary>
    public string Etiqueta => Restricao switch
    {
        Restricao.Contraindicada => "CONTRAINDICADA",
        Restricao.Adiar => "ADIAR",
        Restricao.Avaliar => Prazo == SituacaoPrazo.Atrasada ? "ATRASADA · AVALIAR" : "AVALIAR",
        _ => Prazo switch
        {
            SituacaoPrazo.EmDia => "EM DIA",
            SituacaoPrazo.AplicarHoje => EhRedePrivada ? "DISPONÍVEL" : "APLICAR HOJE",
            SituacaoPrazo.Atrasada => "ATRASADA",
            SituacaoPrazo.AguardandoIntervalo => "AGUARDAR INTERVALO",
            SituacaoPrazo.Futura => "FUTURA",
            SituacaoPrazo.ForaDaFaixaEtaria => "FORA DA FAIXA",
            _ => Prazo.ToString()
        }
    };

    public string DescricaoDoses =>
        ProximaDose is null
            ? $"{DosesRegistradas} dose(s) registrada(s)"
            : $"{DosesRegistradas} de {DosesNecessarias} dose(s) · próxima: {RotuloProximaDose}";
}

public sealed class DoseProjetada
{
    public int NumeroDose { get; set; }
    public DateTime DataPrevista { get; set; }
    public int? RegraId { get; set; }
    public string Via { get; set; } = string.Empty;

    /// <summary>Data em que o paciente atinge a idade mínima desta dose.</summary>
    public DateTime DataIdadeMinima { get; set; }

    /// <summary>Último dia permitido pela idade máxima (null = sem limite).</summary>
    public DateTime? DataLimite { get; set; }

    /// <summary>Intervalo mínimo após a dose anterior (texto da regra, ex.: "28 dias").</summary>
    public string Intervalo { get; set; } = string.Empty;

    /// <summary>Dias mínimos desde a 1ª dose do esquema (0 = sem exigência).</summary>
    public int DiasMinimosDesdePrimeira { get; set; }
}

public sealed class AlertaClinico
{
    public NivelAlerta Nivel { get; set; }
    public string Vacina { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;

    public override string ToString() =>
        string.IsNullOrEmpty(Vacina) ? Mensagem : $"{Vacina}: {Mensagem}";
}

/// <summary>Um atendimento do plano de recuperação (data + vacinas daquele dia).</summary>
public sealed class Atendimento
{
    public int Numero { get; set; }
    public DateTime Data { get; set; }
    public bool EhHoje { get; set; }
    public List<DosePlanejada> Doses { get; set; } = new();
    public List<string> Observacoes { get; set; } = new();
}

public sealed class DosePlanejada
{
    public string Vacina { get; set; } = string.Empty;
    public string ChaveVacina { get; set; } = string.Empty;
    public int NumeroDose { get; set; }
    public bool EhReforco { get; set; }
    public string Rotulo { get; set; } = string.Empty;
    public int? RegraId { get; set; }
    public string Via { get; set; } = string.Empty;
    public bool Atrasada { get; set; }
    public bool RequerAvaliacao { get; set; }

    public override string ToString() =>
        $"{Vacina} — {(string.IsNullOrEmpty(Rotulo) ? (EhReforco ? "reforço" : NumeroDose + "ª dose") : Rotulo)}" +
        (string.IsNullOrEmpty(Via) ? "" : $" ({Via})") +
        (Atrasada ? " [atrasada]" : "") +
        (RequerAvaliacao ? " [avaliar antes]" : "");
}

public sealed class ResumoHistorico
{
    public string Vacina { get; set; } = string.Empty;
    public int Doses { get; set; }
    public DateTime? PrimeiraAplicacao { get; set; }
    public DateTime? UltimaAplicacao { get; set; }
}

/// <summary>Resultado completo da análise de um paciente.</summary>
public sealed class ResultadoAnalise
{
    public int PacienteId { get; set; }
    public string NomePaciente { get; set; } = string.Empty;
    public DateTime DataNascimento { get; set; }
    public DateTime DataAnalise { get; set; }
    public int IdadeMeses { get; set; }
    public string IdadeTexto { get; set; } = string.Empty;
    public string GrupoEtario { get; set; } = string.Empty;
    public List<string> CondicoesConsideradas { get; set; } = new();

    public SituacaoGeral Situacao { get; set; }
    public string VersaoMotor { get; set; } = MotorInfo.Versao;

    /// <summary>Vacinas do calendário do SUS.</summary>
    public List<ItemAnalise> ItensSUS { get; set; } = new();

    /// <summary>Vacinas complementares da rede privada (não entram na situação geral).</summary>
    public List<ItemAnalise> ItensRedePrivada { get; set; } = new();

    public List<Atendimento> PlanoRecuperacao { get; set; } = new();
    public List<AlertaClinico> Alertas { get; set; } = new();
    public List<ResumoHistorico> Historico { get; set; } = new();
    public List<string> Avisos { get; set; } = new();

    // ----- atalhos usados pelas telas e relatórios -----

    public IEnumerable<ItemAnalise> Atrasadas =>
        ItensSUS.Where(i => i.Prazo == SituacaoPrazo.Atrasada && i.Restricao != Restricao.Contraindicada);

    public IEnumerable<ItemAnalise> ParaAplicarHoje =>
        ItensSUS.Where(i => i.Prazo == SituacaoPrazo.AplicarHoje && i.Restricao != Restricao.Contraindicada);

    public IEnumerable<ItemAnalise> Proximas =>
        ItensSUS.Where(i => (i.Prazo == SituacaoPrazo.AguardandoIntervalo || i.Prazo == SituacaoPrazo.Futura)
                            && i.Restricao != Restricao.Contraindicada)
                .OrderBy(i => i.DataMinima ?? DateTime.MaxValue);

    public IEnumerable<ItemAnalise> ComRestricao =>
        ItensSUS.Concat(ItensRedePrivada).Where(i => i.Restricao != Restricao.Nenhuma);

    public IEnumerable<ItemAnalise> EmDia =>
        ItensSUS.Where(i => i.Prazo == SituacaoPrazo.EmDia);

    public IEnumerable<ItemAnalise> ForaDaFaixa =>
        ItensSUS.Where(i => i.Prazo == SituacaoPrazo.ForaDaFaixaEtaria);

    public bool RequerAvaliacao =>
        ComRestricao.Any() || Alertas.Any(a => a.Nivel != NivelAlerta.Informacao);

    public string SituacaoTexto => Situacao switch
    {
        SituacaoGeral.Regular => "REGULAR",
        SituacaoGeral.ComDosesParaAplicar => "COM DOSES PARA APLICAR",
        SituacaoGeral.EmAtraso => "EM ATRASO",
        _ => "NÃO ANALISADO"
    };
}

public static class Calendarios
{
    public const string SUS = "SUS";
    public const string RedePrivada = "Rede Privada";
}

public static class MotorInfo
{
    public const string Versao = "AmarV.Core 1.0 (motor de análise vacinal)";
}
