using System.Text;
using AmarV.Core.Analise;
using AmarV.Core.Entrada;
using AmarV.Core.Relatorios;
using AmarV.Demonstracao;

// =====================================================================
// DEMONSTRAÇÃO DO MOTOR DE ANÁLISE (roda em qualquer computador com .NET 10)
//
//   dotnet run                -> analisa os 10 pacientes fictícios e gera
//                                os relatórios em HTML na pasta "saida"
//   dotnet run -- C05         -> mostra só o cenário C05 no console
// =====================================================================

Console.OutputEncoding = Encoding.UTF8;
string pastaDados = Path.Combine(AppContext.BaseDirectory, "dados");
var regras = CarregadorRegras.DeArquivo(Path.Combine(pastaDados, "regras_sp_2026.json"));
var motor = new MotorAnaliseVacinal(regras);
var prm = new ParametrosAnalise { DataReferencia = Cenarios.Hoje };

string? filtro = args.FirstOrDefault();
var cenarios = Cenarios.Todos().Where(c => filtro == null || c.Codigo.Equals(filtro, StringComparison.OrdinalIgnoreCase)).ToList();

string saida = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "saida"));
Directory.CreateDirectory(saida);

var paraRelatorio = new List<PacienteRelatorio>();
foreach (var c in cenarios)
{
    var r = motor.Analisar(c.Paciente, c.Historico, prm);
    paraRelatorio.Add(new PacienteRelatorio { Analise = r, Telefone = "(11) 90000-00" + c.Paciente.Id.ToString("00"), Endereco = "Endereço fictício" });

    if (filtro != null)
    {
        Console.WriteLine($"################ {c.Codigo} — {c.Titulo}");
        Console.WriteLine(FormatadorTexto.Gerar(r));
    }
    else
    {
        Console.WriteLine($"{c.Codigo}  {r.SituacaoTexto,-24} atrasadas={r.Atrasadas.Count(),2}  hoje={r.ParaAplicarHoje.Count(),2}  " +
                          $"restricoes={r.ComRestricao.Count(),2}  atendimentos={r.PlanoRecuperacao.Count,2}  {c.Titulo}");
    }

    File.WriteAllText(Path.Combine(saida, $"{c.Codigo}_analise.html"), FormatadorHtml.Gerar(r));
    File.WriteAllText(Path.Combine(saida, $"{c.Codigo}_analise.txt"), FormatadorTexto.Gerar(r));
}

foreach (var tipo in Enum.GetValues<TipoRelatorio>())
{
    string nome = tipo switch
    {
        TipoRelatorio.PacientesEmAtraso => "RF20_pacientes_em_atraso",
        TipoRelatorio.PacientesRegulares => "RF21_pacientes_regulares",
        _ => "relatorio_geral"
    };
    File.WriteAllText(Path.Combine(saida, nome + ".html"), RelatorioConsolidado.Html(paraRelatorio, tipo, Cenarios.Hoje.AddHours(10)));
    File.WriteAllText(Path.Combine(saida, nome + ".csv"), RelatorioConsolidado.Csv(paraRelatorio, tipo), new UTF8Encoding(true));
}

// Calendário montado das regras (o que a tela Calendário Vacinal mostra)
var calendario = CalendarioResumo.Montar(regras);
File.WriteAllLines(Path.Combine(saida, "calendario_a_partir_das_regras.txt"),
    calendario.Select(l => $"[{l.Calendario}] {l.GrupoEtario}{(l.GrupoEspecial.Length > 0 ? " / " + l.GrupoEspecial : "")} — {l.Vacina}: {l.Esquema} · faixa {l.Faixa}"));

Console.WriteLine();
Console.WriteLine(RelatorioConsolidado.Resumo(paraRelatorio));
Console.WriteLine($"Relatórios gerados em: {saida}");
