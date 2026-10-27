using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AmarV.Core.Analise;

/// <summary>Cálculos de idade e de intervalos com calendário real (meses e anos de verdade, não "×30 dias").</summary>
public static class Datas
{
    public static int IdadeEmMeses(DateTime nascimento, DateTime referencia)
    {
        if (nascimento == default || referencia < nascimento) return 0;
        int meses = (referencia.Year - nascimento.Year) * 12 + referencia.Month - nascimento.Month;
        if (referencia.Day < nascimento.Day) meses--;
        return Math.Max(0, meses);
    }

    public static string IdadeTexto(DateTime nascimento, DateTime referencia)
    {
        if (nascimento == default) return "idade não informada";
        int meses = IdadeEmMeses(nascimento, referencia);
        int anos = meses / 12, resto = meses % 12;

        if (anos >= 1)
        {
            string a = anos == 1 ? "1 ano" : $"{anos} anos";
            return resto == 0 ? a : $"{a} e {resto} {(resto == 1 ? "mês" : "meses")}";
        }

        int dias = (referencia.Date - nascimento.Date.AddMonths(meses)).Days;
        if (meses == 0) return dias == 1 ? "1 dia" : $"{dias} dias";
        string m = meses == 1 ? "1 mês" : $"{meses} meses";
        return dias == 0 ? m : $"{m} e {dias} {(dias == 1 ? "dia" : "dias")}";
    }

    /// <summary>Data em que a pessoa completa a idade (em meses).</summary>
    public static DateTime DataNaIdade(DateTime nascimento, int meses) =>
        nascimento.Date.AddMonths(Math.Max(0, Math.Min(meses, 1200)));

    /// <summary>Último dia em que a pessoa ainda tem a idade máxima (ex.: "até 11 meses e 29 dias").</summary>
    public static DateTime UltimoDiaNaIdade(DateTime nascimento, int idadeMaximaMeses) =>
        nascimento.Date.AddMonths(Math.Min(idadeMaximaMeses, 1199) + 1).AddDays(-1);

    public static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    public static string FormatarIdadeMeses(int meses)
    {
        if (meses >= 9999) return "sem limite";
        if (meses < 12) return meses == 1 ? "1 mês" : $"{meses} meses";
        int anos = meses / 12, resto = meses % 12;
        string a = anos == 1 ? "1 ano" : $"{anos} anos";
        return resto == 0 ? a : $"{a} e {resto} {(resto == 1 ? "mês" : "meses")}";
    }
}

/// <summary>
/// Interpreta textos de intervalo/periodicidade cadastrados nas regras:
/// "28 dias", "4 semanas", "6 meses", "10 anos", "Anual", "Semestral".
/// </summary>
public readonly record struct Duracao(int Dias, int Meses, int Anos)
{
    public static readonly Duracao Nenhuma = new(0, 0, 0);
    public bool Vazia => Dias == 0 && Meses == 0 && Anos == 0;

    public DateTime SomarA(DateTime data) => data.AddYears(Anos).AddMonths(Meses).AddDays(Dias);

    public override string ToString()
    {
        if (Anos > 0) return Anos == 1 ? "1 ano" : $"{Anos} anos";
        if (Meses > 0) return Meses == 1 ? "1 mês" : $"{Meses} meses";
        if (Dias > 0) return Dias == 1 ? "1 dia" : $"{Dias} dias";
        return "—";
    }

    public static Duracao Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Nenhuma;
        string t = Texto.Normalizar(texto);

        if (t.Contains("anual")) return new(0, 0, 1);
        if (t.Contains("semestral")) return new(0, 6, 0);
        if (t.Contains("mensal")) return new(0, 1, 0);

        var m = Regex.Match(t, @"(\d+)");
        if (!m.Success) return Nenhuma;
        int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);

        if (t.Contains("ano")) return new(0, 0, n);
        if (t.Contains("mes")) return new(0, n, 0);
        if (t.Contains("semana")) return new(n * 7, 0, 0);
        if (t.Contains("dia")) return new(n, 0, 0);
        return Nenhuma;
    }
}

public static class Texto
{
    public static string RemoverAcentos(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);
        foreach (char c in normalizado)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Minúsculas, sem acentos e sem espaços repetidos.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        string t = RemoverAcentos(texto).ToLowerInvariant().Trim();
        return Regex.Replace(t, @"\s+", " ");
    }
}
