namespace AmarV.Core.Entrada;

// =====================================================================
// DADOS DE ENTRADA DO MOTOR DE ANÁLISE
//
// O motor não depende do SQLite nem do MAUI. A tela (ou o serviço do
// aplicativo) converte Paciente, HistoricoVacinal e RegraVacinal para
// estas classes e chama MotorAnaliseVacinal.Analisar(...).
// Assim a regra de negócio pode ser testada fora do celular.
// =====================================================================

/// <summary>Dados do paciente usados na análise (cadastro + condições clínicas).</summary>
public sealed class PerfilPaciente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataNascimento { get; set; }
    public string Sexo { get; set; } = string.Empty;

    // Gestação e amamentação
    public bool Gestante { get; set; }
    public int SemanasGestacao { get; set; }
    public bool Puerpera { get; set; }
    public bool Lactante { get; set; }
    public bool CriancaAmamentadaMenor6Meses { get; set; }

    // Grupos especiais
    public bool ProfissionalSaude { get; set; }
    public bool Indigena { get; set; }
    public bool AcamadoOuInstitucionalizado { get; set; }

    // Condições clínicas
    public bool Imunossuprimido { get; set; }
    public bool HIV { get; set; }
    public bool Quimioterapia { get; set; }
    public bool Transplantado { get; set; }
    public bool UsoCorticoideOuImunobiologico { get; set; }
    public bool AlergiaGrave { get; set; }
    public bool FebreOuDoencaAguda { get; set; }
    public string CondicaoClinica { get; set; } = string.Empty;

    /// <summary>Imunossupressão que contraindica vacinas de agente vivo.</summary>
    public bool TemImunossupressao =>
        Imunossuprimido || Quimioterapia || Transplantado || UsoCorticoideOuImunobiologico;

    /// <summary>Considera também quem amamenta criança menor de 6 meses.</summary>
    public bool Amamentando => Lactante || CriancaAmamentadaMenor6Meses;
}

/// <summary>Um registro do histórico vacinal (normalmente uma linha = uma dose).</summary>
public sealed class DoseAplicada
{
    public int Id { get; set; }
    public string NomeVacina { get; set; } = string.Empty;
    public int RegraVacinalId { get; set; }

    /// <summary>Quantidade de doses representadas pela linha (registros antigos podem ter mais de 1).</summary>
    public int Quantidade { get; set; } = 1;

    /// <summary>Data da aplicação. Em registros antigos com várias doses, é a data da última.</summary>
    public DateTime? Data { get; set; }

    public string Lote { get; set; } = string.Empty;
    public string LocalAplicacao { get; set; } = string.Empty;
    public string Profissional { get; set; } = string.Empty;
}

/// <summary>Uma regra do calendário (mesmos campos de RegraVacinal do aplicativo).</summary>
public sealed class RegraCalendario
{
    public int Id { get; set; }
    public string NomeVacina { get; set; } = string.Empty;
    public int NumeroDose { get; set; }
    public int TotalDoses { get; set; }
    public int IdadeMinimaMeses { get; set; }
    public int IdadeMaximaMeses { get; set; }
    public int IdadeReferenciaMeses { get; set; }
    public string GrupoEtario { get; set; } = string.Empty;
    public string GrupoEspecial { get; set; } = string.Empty;
    public int SemanaGestacionalMinima { get; set; }
    public string IntervaloEntreDoses { get; set; } = string.Empty;
    public string Periodicidade { get; set; } = string.Empty;
    public string ViaAdministracao { get; set; } = string.Empty;
    public string LocalAplicacao { get; set; } = string.Empty;
    public string TipoCalendario { get; set; } = string.Empty;
    public string FonteCalendario { get; set; } = string.Empty;
    public string Indicacao { get; set; } = string.Empty;
    public string Contraindicacao { get; set; } = string.Empty;
    public string EventosAdversos { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;

    /// <summary>Idade máxima efetiva (0 ou negativo = sem limite).</summary>
    public int IdadeMaximaEfetiva => IdadeMaximaMeses <= 0 ? 9999 : IdadeMaximaMeses;

    /// <summary>Idade recomendada; quando não informada, usa a idade mínima.</summary>
    public int IdadeRecomendadaMeses =>
        IdadeReferenciaMeses > 0 ? Math.Max(IdadeReferenciaMeses, IdadeMinimaMeses) : IdadeMinimaMeses;
}
