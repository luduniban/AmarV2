# Amar V — versão 2.1 (motor de análise vacinal)

Esta pasta é uma **cópia nova** do projeto. Ela parte do `maui_amarv_revisado6` (Ludgerio, 25/10/2026), com as correções de login e cadastro de 26/10 e um motor de análise vacinal reconstruído. Os arquivos originais de vocês não foram alterados.

Para entender **o que estava errado e o que mudou**, leia `docs/DIAGNOSTICO_E_MELHORIAS.md`.

## Estrutura

```
AmarV_v2/
├── AmarV.slnx                      ← abra este arquivo no Visual Studio
├── MauiApp1/                       ← aplicativo MAUI (revisado6 + correções + integração)
├── src/AmarV.Core/                 ← MOTOR de análise (sem MAUI/SQLite; testável)
│   ├── Analise/                    ← regras de decisão, esquemas, plano de recuperação, segurança
│   ├── Entrada/                    ← dados de entrada (paciente, doses, regras)
│   ├── Resultado/                  ← resultado estruturado (situação, itens, atendimentos, alertas)
│   └── Relatorios/                 ← texto, HTML, CSV, RF20/RF21, calendário, colunas do banco
├── tests/AmarV.Core.Testes/        ← 49 testes automatizados
├── tools/AmarV.Demonstracao/       ← roda 10 pacientes fictícios e gera relatórios HTML/CSV
│   ├── dados/regras_sp_2026.json   ← as 132 regras do amarv 7.sql (SP-2026.05)
│   └── saida/                      ← relatórios já gerados (evidência para a Agenda)
└── docs/
    ├── DIAGNOSTICO_E_MELHORIAS.md  ← análise completa
    ├── comparacao/                 ← saída do motor antigo × motor novo (mesmos pacientes)
    └── (MauiApp1/docs/legado/)     ← motor antigo guardado para consulta
```

## 1. Rodar o motor e os testes (qualquer computador com .NET 10)

No terminal, dentro da pasta `AmarV_v2`:

```
dotnet run --project tests/AmarV.Core.Testes
dotnet run --project tools/AmarV.Demonstracao
dotnet run --project tools/AmarV.Demonstracao -- C05
```

- O primeiro comando deve terminar com "49 teste(s) OK, 0 falha(s)".
- O segundo gera os relatórios HTML na pasta `tools/AmarV.Demonstracao/saida`.
- O terceiro mostra um paciente no console. Os cenários vão de C01 a C10.

## 2. Abrir e rodar o app no Visual Studio

1. Abra `AmarV.slnx`. A solução tem 4 projetos: o app, o motor, os testes e a demonstração.
2. Defina **maui_amarv** como projeto de inicialização.
3. **Compilar → Limpar solução** e depois **Recompilar**. Se aparecerem erros de pacote, use **Restaurar pacotes NuGet**.
4. Rode no Windows (`net10.0-windows…`) ou no celular Android.
5. Se o celular já tinha uma versão anterior, **limpe os dados do app** (Configurações → Aplicativos → Amar V → Armazenamento → Limpar dados) ou desinstale. As contas iniciais são criadas com o banco vazio: `admin` / `123` e `usuario` / `123`, com troca de senha no primeiro acesso.

> Os avisos CS0618 (`DisplayAlert` obsoleto no MAUI 10) já existiam e não impedem a compilação.
>
> As telas MAUI novas não puderam ser compiladas no ambiente onde foram feitas (sem o workload do MAUI). Se o Visual Studio apontar algum erro nelas, o arquivo e a linha indicados no erro bastam para corrigir.

## 3. Roteiro de teste no app (perfil **usuario**)

1. **Cadastrar Paciente**: crie, por exemplo, uma criança de 4 anos e uma adulta de 35 anos lactante (marque "lactante" e "amamenta criança < 6 meses").
2. **Histórico Vacinal**: lance algumas doses da criança (BCG, Penta 1-2-3, VIP 1-2-3).
3. **Análise Vacinal**: escolha o paciente e confira:
   - a situação no topo (EM ATRASO, COM DOSES PARA APLICAR ou REGULAR);
   - os cartões vermelhos (atrasadas) e laranja (aplicar hoje);
   - o "Esquema de recuperação" com atendimentos e datas;
   - na adulta lactante, a febre amarela em roxo (**ADIAR**), que fica fora do plano.
4. Toque em **"Registrar 1ª dose aplicada hoje"** numa vacina, informe o lote e veja a análise se atualizar sozinha.
5. Teste **Compartilhar** (texto) e **Relatório** (HTML; abra no navegador e imprima ou salve em PDF).
6. Abra **Calendário Vacinal** e use os filtros SUS / Rede Privada e Criança / Adulto / Gestante.

Depois, entre como **admin** e abra **Relatórios**:

7. Gere **Pacientes com Vacinas Atrasadas** e **Pacientes em Situação Regular** e confira se batem com a tela de análise.
8. Gere **Exportar Excel/CSV**.

## 4. Levar as mudanças para outra versão do projeto

Se o Ludgerio tiver uma versão mais nova do MauiApp1, siga estes passos:

1. Copie a pasta `src/AmarV.Core` para a raiz do repositório.
2. No `maui_amarv.csproj`, adicione `<ProjectReference Include="..\src\AmarV.Core\AmarV.Core.csproj" />` e remova a linha `<Compile Include="C:\Users\jesui\...SenhaHelper.cs" ...>`.
3. Substitua estes arquivos: `Services/AnaliseEsquemaVacinal.cs`, `Models/AnaliseEsquemaVacinal.cs`, `Views/AnaliseVacinalPage.xaml(.cs)`, `Views/RelatorioPage.xaml.cs` e `Views/CalendarioVacinal.xaml(.cs)`.

As outras telas não dependem dessas mudanças.

## 5. Parâmetros que o PO pode ajustar

Ficam em `MauiApp1/Services/AnaliseEsquemaVacinal.cs → CriarParametros()`:

| Parâmetro | Padrão | Efeito |
|---|---|---|
| `ToleranciaAtrasoDias` | 30 | Dias após a data recomendada até a dose virar "atrasada" |
| `HorizonteMeses` | 12 | Até quando mostrar as vacinas futuras e planejar os atendimentos |
| `AplicarLimitesPorAtendimento` | true | Liga ou desliga o limite de aplicações por atendimento |
| `LimiteSubcutaneas` | 2 | Máximo de SC por atendimento |
| `LimiteIntramuscularesAte24Meses` | 2 | Máximo de IM até 24 meses (a visita dos 2 meses tem 3 IM: avaliar) |
| `LimiteIntramuscularesAcima24Meses` | 4 | Máximo de IM acima de 24 meses |

## Antes de entregar ao professor

- Não inclua as pastas `bin/` e `obj/`.
- No pacote do servidor PHP, retire `backups/` (`.db3` e JSONs com dados) e `config.local.php`.
- Troque as senhas de teste.
