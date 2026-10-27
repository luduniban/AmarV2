using AmarV.Core.Entrada;

namespace AmarV.Demonstracao;

/// <summary>
/// Pacientes fictícios usados na demonstração e nos testes.
/// A data de referência é fixa para que o resultado seja sempre o mesmo.
/// </summary>
public static class Cenarios
{
    public static readonly DateTime Hoje = new(2026, 10, 26);

    public sealed record Cenario(string Codigo, string Titulo, PerfilPaciente Paciente, List<DoseAplicada> Historico);

    private static DoseAplicada D(string vacina, DateTime data) => new() { NomeVacina = vacina, Data = data, Quantidade = 1 };

    public static List<Cenario> Todos()
    {
        var lista = new List<Cenario>();

        // 1. Bebê de 2 meses sem nenhuma vacina registrada
        var nasc1 = Hoje.AddMonths(-2).AddDays(-6);
        lista.Add(new("C01", "Bebê de 2 meses sem vacinas",
            new PerfilPaciente { Id = 1, Nome = "Helena Souza (fictícia)", DataNascimento = nasc1, Sexo = "F" },
            new()));

        // 2. Bebê de 2 meses com BCG e hepatite B ao nascer
        var nasc2 = Hoje.AddMonths(-2).AddDays(-3);
        lista.Add(new("C02", "Bebê de 2 meses com BCG e hepatite B da maternidade",
            new PerfilPaciente { Id = 2, Nome = "Miguel Lima (fictício)", DataNascimento = nasc2, Sexo = "M" },
            new() { D("BCG", nasc2.AddDays(1)), D("Hepatite B", nasc2) }));

        // 3. Criança de 4 anos e 3 meses com caderneta parcial (parou após 1 ano)
        var nasc3 = Hoje.AddYears(-4).AddMonths(-3);
        lista.Add(new("C03", "Criança de 4 anos com caderneta incompleta",
            new PerfilPaciente { Id = 3, Nome = "Davi Rocha (fictício)", DataNascimento = nasc3, Sexo = "M" },
            new()
            {
                D("BCG", nasc3.AddDays(2)), D("Hepatite B", nasc3),
                D("Pentavalente", nasc3.AddMonths(2)), D("Pentavalente", nasc3.AddMonths(4)), D("Pentavalente", nasc3.AddMonths(6)),
                D("VIP", nasc3.AddMonths(2)), D("VIP", nasc3.AddMonths(4)), D("VIP", nasc3.AddMonths(6)),
                D("Rotavírus", nasc3.AddMonths(2)), D("Rotavírus", nasc3.AddMonths(4)),
                D("Pneumocócica 10-valente", nasc3.AddMonths(2)), D("Pneumocócica 10-valente", nasc3.AddMonths(4)),
                D("Pneumocócica 10-valente", nasc3.AddMonths(12)),
                D("Meningocócica C", nasc3.AddMonths(3)), D("Meningocócica C", nasc3.AddMonths(5)),
                D("Febre Amarela", nasc3.AddMonths(9)), D("SCR", nasc3.AddMonths(12)),
            }));

        // 4. Criança de 1 ano e 6 meses com tudo em dia
        var nasc4 = Hoje.AddMonths(-18);
        lista.Add(new("C04", "Criança de 1 ano e 6 meses com caderneta em dia",
            new PerfilPaciente { Id = 4, Nome = "Alice Prado (fictícia)", DataNascimento = nasc4, Sexo = "F" },
            new()
            {
                D("BCG", nasc4.AddDays(1)), D("Hepatite B", nasc4),
                D("Pentavalente", nasc4.AddMonths(2)), D("Pentavalente", nasc4.AddMonths(4)), D("Pentavalente", nasc4.AddMonths(6)),
                D("VIP", nasc4.AddMonths(2)), D("VIP", nasc4.AddMonths(4)), D("VIP", nasc4.AddMonths(6)), D("VIP", nasc4.AddMonths(15)),
                D("Rotavírus", nasc4.AddMonths(2)), D("Rotavírus", nasc4.AddMonths(4)),
                D("Pneumocócica 10-valente", nasc4.AddMonths(2)), D("Pneumocócica 10-valente", nasc4.AddMonths(4)),
                D("Pneumocócica 10-valente", nasc4.AddMonths(12)),
                D("Meningocócica C", nasc4.AddMonths(3)), D("Meningocócica C", nasc4.AddMonths(5)),
                D("Meningocócica ACWY", nasc4.AddMonths(12)),
                D("Influenza", Hoje.AddMonths(-6)), D("Influenza", Hoje.AddMonths(-5)),
                D("COVID-19", nasc4.AddMonths(6)), D("COVID-19", nasc4.AddMonths(7)), D("COVID-19", nasc4.AddMonths(9)),
                D("Febre Amarela", nasc4.AddMonths(9)),
                D("SCR", nasc4.AddMonths(12)), D("SCR", nasc4.AddMonths(15)),
                D("Varicela", nasc4.AddMonths(15)), D("Hepatite A", nasc4.AddMonths(15)), D("DTP", nasc4.AddMonths(15)),
            }));

        // 5. Adulta de 35 anos, lactante de bebê < 6 meses, sem histórico (cenário do teste de 09/10/2026)
        lista.Add(new("C05", "Adulta de 35 anos, lactante (bebê < 6 meses), sem histórico",
            new PerfilPaciente
            {
                Id = 5, Nome = "Juliana Matos (fictícia)", DataNascimento = Hoje.AddYears(-35).AddMonths(-2), Sexo = "F",
                Lactante = true, CriancaAmamentadaMenor6Meses = true
            }, new()));

        // 6. Gestante de 30 anos, 29 semanas, dT completa há 5 anos e hepatite B completa
        var nasc6 = Hoje.AddYears(-30).AddMonths(-4);
        lista.Add(new("C06", "Gestante de 30 anos com 29 semanas",
            new PerfilPaciente
            {
                Id = 6, Nome = "Fernanda Alves (fictícia)", DataNascimento = nasc6, Sexo = "F",
                Gestante = true, SemanasGestacao = 29
            },
            new()
            {
                D("dT", Hoje.AddYears(-6)), D("dT", Hoje.AddYears(-6).AddMonths(2)), D("dT", Hoje.AddYears(-5)),
                D("Hepatite B", Hoje.AddYears(-8)), D("Hepatite B", Hoje.AddYears(-8).AddMonths(1)), D("Hepatite B", Hoje.AddYears(-7).AddMonths(-6)),
                D("SCR", nasc6.AddMonths(12)), D("SCR", nasc6.AddMonths(15)),
            }));

        // 7. Idoso de 68 anos: dT há 12 anos, influenza há 18 meses
        lista.Add(new("C07", "Idoso de 68 anos com reforço de dT vencido",
            new PerfilPaciente { Id = 7, Nome = "José Ribeiro (fictício)", DataNascimento = Hoje.AddYears(-68).AddDays(-40), Sexo = "M" },
            new()
            {
                D("dT", Hoje.AddYears(-32)), D("dT", Hoje.AddYears(-32).AddMonths(2)), D("dT", Hoje.AddYears(-12)),
                D("Influenza", Hoje.AddMonths(-18)),
            }));

        // 8. Adolescente de 12 anos em quimioterapia, sem HPV/ACWY
        var nasc8 = Hoje.AddYears(-12).AddMonths(-1);
        lista.Add(new("C08", "Adolescente de 12 anos em quimioterapia",
            new PerfilPaciente
            {
                Id = 8, Nome = "Lucas Pereira (fictício)", DataNascimento = nasc8, Sexo = "M",
                Quimioterapia = true, Imunossuprimido = true
            },
            new()
            {
                D("Hepatite B", nasc8), D("Pentavalente", nasc8.AddMonths(2)), D("Pentavalente", nasc8.AddMonths(4)),
                D("Pentavalente", nasc8.AddMonths(6)), D("DTP", nasc8.AddMonths(15)), D("DTP", nasc8.AddYears(4)),
                D("VIP", nasc8.AddMonths(2)), D("VIP", nasc8.AddMonths(4)), D("VIP", nasc8.AddMonths(6)), D("VIP", nasc8.AddMonths(15)),
                D("SCR", nasc8.AddMonths(12)), D("SCR", nasc8.AddMonths(15)),
                D("Febre Amarela", nasc8.AddMonths(9)), D("Febre Amarela", nasc8.AddYears(4)),
            }));

        // 9. Criança de 3 anos com febre no dia do atendimento, sem vacinas após 1 ano
        var nasc9 = Hoje.AddYears(-3);
        lista.Add(new("C09", "Criança de 3 anos com febre no atendimento",
            new PerfilPaciente { Id = 9, Nome = "Sofia Nunes (fictícia)", DataNascimento = nasc9, Sexo = "F", FebreOuDoencaAguda = true },
            new()
            {
                D("BCG", nasc9), D("Hepatite B", nasc9),
                D("Pentavalente", nasc9.AddMonths(2)), D("Pentavalente", nasc9.AddMonths(4)), D("Pentavalente", nasc9.AddMonths(6)),
                D("VIP", nasc9.AddMonths(2)), D("VIP", nasc9.AddMonths(4)), D("VIP", nasc9.AddMonths(6)),
            }));

        // 10. Profissional de saúde de 28 anos com 1 dose de SCR na infância e hepatite B completa
        var nasc10 = Hoje.AddYears(-28);
        lista.Add(new("C10", "Profissional de saúde de 28 anos",
            new PerfilPaciente { Id = 10, Nome = "Rafael Costa (fictício)", DataNascimento = nasc10, Sexo = "M", ProfissionalSaude = true },
            new()
            {
                D("SCR", nasc10.AddMonths(12)),
                D("Hepatite B", Hoje.AddYears(-3)), D("Hepatite B", Hoje.AddYears(-3).AddMonths(1)), D("Hepatite B", Hoje.AddYears(-3).AddMonths(6)),
                D("dT", Hoje.AddYears(-9)), D("dT", Hoje.AddYears(-9).AddMonths(2)), D("dT", Hoje.AddYears(-8)),
                D("Febre Amarela", Hoje.AddYears(-10)),
                D("Influenza", Hoje.AddMonths(-7)),
            }));

        return lista;
    }
}
