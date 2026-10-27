namespace AmarV.Core.Analise;

/// <summary>
/// Parâmetros operacionais da análise. Ficam separados das regras do
/// calendário para que o Product Owner possa validá-los e alterá-los
/// sem mexer no código do motor.
/// </summary>
public sealed class ParametrosAnalise
{
    /// <summary>Data usada como "hoje". Nos testes, é fixada para o resultado ser reproduzível.</summary>
    public DateTime DataReferencia { get; set; } = DateTime.Today;

    /// <summary>
    /// Dias de tolerância após a data recomendada antes de classificar a dose
    /// como ATRASADA. Dentro da tolerância, a dose aparece como APLICAR HOJE.
    /// </summary>
    public int ToleranciaAtrasoDias { get; set; } = 30;

    /// <summary>Quantos meses à frente mostrar vacinas futuras e montar o plano de recuperação.</summary>
    public int HorizonteMeses { get; set; } = 12;

    /// <summary>
    /// Limite de aplicações por via no mesmo atendimento (regra operacional definida
    /// pela equipe; não é protocolo oficial). Desligue com false para não limitar.
    /// </summary>
    public bool AplicarLimitesPorAtendimento { get; set; } = true;
    public int LimiteSubcutaneas { get; set; } = 2;
    public int LimiteIntramuscularesAte24Meses { get; set; } = 2;
    public int LimiteIntramuscularesAcima24Meses { get; set; } = 4;

    /// <summary>Retorno sugerido (dias) para doses que não couberam no atendimento.</summary>
    public int RetornoDias { get; set; } = 7;

    /// <summary>
    /// Doses futuras com datas próximas (até N dias) são juntadas no mesmo atendimento,
    /// adiando a mais cedo. Adiar uma dose não invalida o esquema; antecipar, sim.
    /// </summary>
    public int JanelaAgrupamentoDias { get; set; } = 10;

    /// <summary>Intervalo entre vacinas vivas injetáveis não aplicadas no mesmo dia (Norma SP 2025: 4 semanas).</summary>
    public int IntervaloVivasInjetaveisDias { get; set; } = 28;

    /// <summary>Mostrar vacinas cuja idade máxima já passou (apenas para menores de 10 anos).</summary>
    public bool MostrarForaDaFaixa { get; set; } = true;

    /// <summary>Incluir vacinas da rede privada no resultado (lista separada).</summary>
    public bool IncluirRedePrivada { get; set; } = true;
}
