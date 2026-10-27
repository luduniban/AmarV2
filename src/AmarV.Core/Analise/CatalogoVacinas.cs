using AmarV.Core.Entrada;
using AmarV.Core.Resultado;

namespace AmarV.Core.Analise;

/// <summary>
/// Conhecimento fixo sobre as vacinas que não está nas regras do banco:
/// nomes equivalentes, quais vacinas contam doses para outras (ex.: a
/// pentavalente conta como dose de hepatite B e de dT) e quais são de
/// agente vivo. Tudo centralizado aqui para o Product Owner validar.
/// </summary>
public static class CatalogoVacinas
{
    // ------------------------------------------------------------------
    // CHAVES (nome normalizado de cada vacina)
    // ------------------------------------------------------------------
    public const string BCG = "bcg";
    public const string HepatiteB = "hepatite b";
    public const string HepatiteA = "hepatite a";
    public const string Pentavalente = "pentavalente";
    public const string Hexavalente = "hexavalente";
    public const string VIP = "vip";
    public const string VOP = "vop";
    public const string Rotavirus = "rotavirus";
    public const string Pneumo10 = "pneumococica 10-valente";
    public const string Pneumo13 = "pneumococica 13";
    public const string Pneumo23 = "pneumococica 23-valente";
    public const string MeningoC = "meningococica c";
    public const string MeningoACWY = "meningococica acwy";
    public const string MeningoB = "meningococica b";
    public const string Influenza = "influenza";
    public const string Covid = "covid-19";
    public const string FebreAmarela = "febre amarela";
    public const string SCR = "scr";
    public const string Tetraviral = "tetraviral";
    public const string Varicela = "varicela";
    public const string DTP = "dtp";
    public const string DT = "dt";
    public const string DTPa = "dtpa";
    public const string HPV = "hpv";
    public const string Dengue = "dengue";
    public const string VSR = "vsr";

    /// <summary>Converte qualquer grafia de nome para a chave da vacina.</summary>
    public static string Chave(string? nome)
    {
        string n = Texto.Normalizar(nome);
        if (n.Length == 0) return string.Empty;

        // remove pontuação comum
        n = n.Replace("(", " ").Replace(")", " ").Replace("  ", " ").Trim();

        if (n == "bcg" || n.StartsWith("bcg ")) return BCG;
        if (n.StartsWith("hepatite b") || n is "hepb" or "hep b" or "vhb") return HepatiteB;
        if (n.StartsWith("hepatite a") || n is "hepa" or "hep a") return HepatiteA;
        if (n.StartsWith("penta")) return Pentavalente;
        if (n.StartsWith("hexa")) return Hexavalente;
        if (n == "vip" || n.StartsWith("vip ") || n.Contains("poliomielite inativada")) return VIP;
        if (n == "vop" || n == "vopb" || n.StartsWith("vop ") || n.Contains("poliomielite oral")) return VOP;
        if (n.StartsWith("rotavirus") || n is "vrh" or "rota") return Rotavirus;

        if (n.StartsWith("pneumococica") || n.StartsWith("pneumo") || n.StartsWith("vpc") || n.StartsWith("vpp"))
        {
            if (n.Contains("23")) return Pneumo23;
            if (n.Contains("13")) return Pneumo13;
            // A 20-valente foi substituída pela 10-valente na Norma SP 2025 (mesma regra do BancoDados.ChaveRegra).
            if (n.Contains("10") || n.Contains("20")) return Pneumo10;
            return n;
        }

        if (n.StartsWith("meningococica") || n.StartsWith("meningo") || n.StartsWith("men"))
        {
            if (n.Contains("acwy")) return MeningoACWY;
            if (n.EndsWith(" b") || n.Contains(" b ") || n == "menb") return MeningoB;
            if (n.EndsWith(" c") || n.Contains(" c ") || n == "menc" || n.Contains("conjugada c")) return MeningoC;
            return n;
        }

        if (n.StartsWith("influenza") || n.StartsWith("gripe")) return Influenza;
        if (n.StartsWith("covid")) return Covid;
        if (n.StartsWith("febre amarela") || n == "fa" || n == "vfa") return FebreAmarela;
        if (n.StartsWith("tetraviral") || n == "scrv") return Tetraviral;
        if (n == "scr" || n.StartsWith("scr ") || n.StartsWith("triplice viral") || n == "sarampo, caxumba e rubeola")
            return SCR;
        if (n.StartsWith("varicela") || n == "vz") return Varicela;
        if (n == "dtpa" || n.StartsWith("dtpa ") || n.StartsWith("dtp acelular") || n.Contains("triplice bacteriana acelular"))
            return DTPa;
        if (n == "dtp" || n.StartsWith("dtp ") || n.StartsWith("triplice bacteriana")) return DTP;
        if (n == "dt" || n.StartsWith("dt ") || n.StartsWith("dupla adulto") || n.StartsWith("dupla bacteriana"))
            return DT;
        if (n.StartsWith("hpv")) return HPV;
        if (n.StartsWith("dengue")) return Dengue;
        if (n.StartsWith("vsr") || n.Contains("sincicial")) return VSR;

        return n;
    }

    /// <summary>
    /// Para quais esquemas uma dose aplicada conta. Ex.: 1 dose de pentavalente
    /// conta como dose de pentavalente, de hepatite B e do esquema básico de dT.
    /// </summary>
    public static IReadOnlyCollection<string> ContaComo(string chave) => chave switch
    {
        Pentavalente => new[] { Pentavalente, HepatiteB, DT },
        Hexavalente => new[] { Pentavalente, HepatiteB, DT, VIP },
        DTP => new[] { DTP, DT },
        DTPa => new[] { DTPa, DT },
        Tetraviral => new[] { SCR, Varicela },
        VOP => new[] { VIP },
        _ => new[] { chave }
    };

    private static readonly HashSet<string> Vivas = new()
    {
        BCG, Rotavirus, SCR, Tetraviral, Varicela, FebreAmarela, Dengue, VOP
    };

    private static readonly HashSet<string> VivasInjetaveis = new()
    {
        SCR, Tetraviral, Varicela, FebreAmarela, Dengue
    };

    public static bool EhViva(string chave) => Vivas.Contains(chave);
    public static bool EhVivaInjetavel(string chave) => VivasInjetaveis.Contains(chave);

    /// <summary>Normaliza a via: "IM", "SC", "ID", "Oral" ou vazio.</summary>
    public static string Via(string? via)
    {
        string t = Texto.Normalizar(via).Replace(".", "").Replace("-", " ");
        if (t is "im" or "i m" || t.Contains("intramuscular")) return "IM";
        if (t is "sc" or "s c" || t.Contains("subcutanea")) return "SC";
        if (t is "id" or "i d" || t.Contains("intradermica")) return "ID";
        if (t is "oral" or "vo" or "v o" || t.Contains("oral")) return "Oral";
        return string.Empty;
    }

    public static string Calendario(string? tipo)
    {
        string t = Texto.Normalizar(tipo);
        if (t.Contains("privad") || t.Contains("particular")) return Calendarios.RedePrivada;
        return Calendarios.SUS;
    }

    public static bool EhGrupoGestante(string? grupoEtario) =>
        Texto.Normalizar(grupoEtario).Contains("gestante");

    public static bool EhGrupoGeral(string? grupoEspecial)
    {
        string g = Texto.Normalizar(grupoEspecial);
        return g.Length == 0 || g == "geral" || g == "todos";
    }

    /// <summary>O paciente pertence ao grupo especial exigido pela regra?</summary>
    public static bool AtendeGrupoEspecial(string? grupoEspecial, PerfilPaciente p)
    {
        string g = Texto.Normalizar(grupoEspecial);
        if (EhGrupoGeral(g)) return true;

        if (g.Contains("gestante")) return p.Gestante;
        if (g.Contains("puerpera")) return p.Puerpera;
        if (g.Contains("prioritario"))
            return p.Gestante || p.Puerpera || p.ProfissionalSaude || p.Indigena ||
                   p.AcamadoOuInstitucionalizado || p.TemImunossupressao || p.HIV;
        if (g.Contains("profissional")) return p.ProfissionalSaude;
        if (g.Contains("indigena")) return p.Indigena;
        if (g.Contains("acamado") || g.Contains("institucional")) return p.AcamadoOuInstitucionalizado;
        if (g.Contains("imuno") || g.Contains("crie"))
            return p.TemImunossupressao || p.HIV;
        if (g.Contains("grupo especial"))
            return p.ProfissionalSaude || p.Indigena || p.AcamadoOuInstitucionalizado;

        // Grupo desconhecido: não aplica (evita indicar vacina para quem não é do grupo).
        return false;
    }

    /// <summary>
    /// Intervalo mínimo contado a partir da 1ª dose (além do intervalo entre doses da regra).
    /// Hepatite B: a 3ª dose exige ao menos 16 semanas desde a 1ª (esquema 0, 1 e 6 meses;
    /// mínimos de 4 semanas entre a 1ª e a 2ª, 8 entre a 2ª e a 3ª e 16 entre a 1ª e a 3ª).
    /// </summary>
    public static int DiasMinimosDesdeAPrimeiraDose(string chave, int numeroDose) =>
        chave == HepatiteB && numeroDose == 3 ? 112 : 0;

    public static string GrupoEtario(int idadeMeses, bool gestante)
    {
        string g = idadeMeses < 120 ? "Criança"
                 : idadeMeses < 300 ? "Adolescente/Jovem"
                 : idadeMeses < 720 ? "Adulto"
                 : "Idoso";
        return gestante ? $"Gestante ({g.ToLowerInvariant()})" : g;
    }
}
