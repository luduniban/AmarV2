# Amar V — Diagnóstico do projeto e melhorias aplicadas

Análise feita em 26/10/2026 sobre a versão **maui_amarv_revisado6** (enviada pelo Ludgerio em 25/10), mais as correções de login e cadastro do pacote `AmarV_correcoes_para_Ludgerio.zip`, o banco `amarv 7.sql` (132 regras, calendário SP-2026.05), o servidor PHP (`amarv 5.zip`) e as Agendas 1 e 5.

---

## 1. O que o aplicativo precisa entregar

Pela Declaração de Visão, o Amar V existe para que o profissional de saúde, diante de um paciente com caderneta incompleta ou sem caderneta, saiba **em poucos segundos**:

1. a situação vacinal (regular ou em atraso);
2. **quais vacinas estão atrasadas** e quais estão pendentes ou são as próximas (RF09, RF10);
3. **o que aplicar hoje e quando voltar** (esquema de recuperação, RF13);
4. o que **não** aplicar, ou só aplicar após avaliação (contraindicações, RF14);
5. o que existe no SUS e na rede privada (RF11, RF12).

Além disso, o administrador precisa de relatórios de pacientes atrasados e regulares (RF20, RF21).

Tudo isso depende de um único componente, a **análise do esquema vacinal (RF08)**. Se ela erra, todo o resto erra junto.

## 2. O que encontrei

As telas, o banco, o login, a sincronização e o backup estão bem encaminhados. O cadastro de 132 regras da Norma Técnica SP 2025 está detalhado e é um ponto forte do trabalho. O problema está no centro do app: a análise não chega ao resultado que o objetivo pede. Para confirmar isso com fatos, rodei o **motor do revisado6 sem alterações** num simulador, com pacientes de teste (a saída completa está em `docs/comparacao/motor_antigo_revisado6.txt`).

### 2.1 A análise quase nunca diz "atrasada"

O motor antigo conta quantas doses o paciente tem, mas **não compara com a idade ou data em que cada dose deveria ter sido aplicada**. O resultado:

| Paciente de teste | O que o motor antigo mostrou | O correto |
|---|---|---|
| Bebê de 2 meses sem BCG | "BCG — LIBERADA PARA ESTE ATENDIMENTO" | BCG **atrasada** (é aplicada ao nascer) |
| Adulta de 35 anos sem nenhuma vacina | Hepatite B, dT e SCR "LIBERADA…" | As três **atrasadas** há anos |
| Idoso sem hepatite B | "LIBERADA…" | **Atrasada** |

Como "atrasada" quase não aparece, a situação geral do paciente e os relatórios ficam sem base.

### 2.2 "Nenhuma dose futura identificada" para um bebê de 2 meses

O filtro de regras descarta as doses cuja idade mínima ainda não chegou, **antes** de checar se são futuras. Por isso a Meningo C dos 3 meses, a Penta dos 4 meses, a febre amarela dos 9 meses e outras nunca aparecem como próximas. O mesmo defeito foi registrado no teste de 09/10/2026 (Agenda 5, seção 3.4.2).

### 2.3 A recuperação não tem datas

A saída era do tipo "Hepatite B: iniciar esquema de 3 dose(s)", sem dizer quando aplicar a 2ª e a 3ª dose nem como encaixar as vacinas nos atendimentos.

### 2.4 Os relatórios administrativos usam outra lógica, e ela está errada

`RelatorioPage.CalcularPendencias` não usa a análise. Ela:

- compara **cada regra** com o histórico pelo `RegraVacinalId`. Como cada dose é uma linha com `DosesTomadas = 1` e a regra tem `TotalDoses = 3`, toda vacina de várias doses fica **sempre pendente**, mesmo com o esquema completo;
- usa `ListarRegrasPorIdade`, que traz **todas** as regras da faixa de idade, inclusive as de gestante (para homens), indígena, profissional de saúde e rede privada.

Na prática, quase ninguém aparece como "regular", e o relatório pode discordar da tela de análise para o mesmo paciente.

### 2.5 A saída é só texto

`AnalisarPacienteAsync` devolve uma `string` de cerca de 100 linhas. Nada é estruturado, e por isso:

- a tela só consegue mostrar um texto corrido;
- o relatório não reaproveita a análise (daí a lógica paralela do item 2.4);
- as colunas `VacinasEmAtraso`, `VacinasPendentes`, `SugestaoRecuperacao`, `VacinasContraindicadas` e as demais da tabela `analise_esquema_vacinal` do MariaDB **nunca são preenchidas**.

### 2.6 Outros pontos encontrados

| # | Onde | Problema |
|---|---|---|
| a | `maui_amarv.csproj` | `<Compile Include="C:\Users\jesui\...\SenhaHelper.cs">`: caminho absoluto da máquina do Ludgerio. Em outro computador, o projeto não compila. |
| b | `Resources/Fonts` | Pasta vazia no revisado6, embora `MauiProgram` registre as fontes OpenSans. |
| c | `RelatorioPage.xaml` | O seletor de paciente usa `{Binding Nome}`, mas `Paciente` não tem `Nome`, então a lista mostra o nome da classe. |
| d | `RelatorioPage` (regulares) | Salva o relatório **dentro do laço**: um registro por paciente. |
| e | Regras 250 e 260 (dT adulto e idoso, 3ª dose) | Sem `IntervaloEntreDoses`. O motor antigo aplicaria a 3ª dose no dia seguinte à 2ª. |
| f | Hepatite B | Falta a regra "3ª dose ≥ 16 semanas após a 1ª". Só com os intervalos cadastrados (28 e 60 dias), a 3ª dose cairia antes do mínimo. |
| g | Limite operacional | 2 IM para menores de 2 anos. A visita de rotina dos 2 meses tem **3 IM** (Penta, VIP e Pneumo 10), então o próprio calendário estoura o limite. **Decisão do PO**: manter 2, passar para 3 ou desligar. |
| h | Calendário (RF05) | A tela do usuário mostra texto fixo. Alterações do administrador não aparecem. |
| i | Pacote do servidor | O `amarv 5.zip` inclui `backups/` com `.db3` e JSONs de dados, além de `config.local.php`. Não entregar assim ao professor. |
| j | `Febre amarela + lactante` (teste de 09/10) | Já corrigido no revisado6. Mantive a regra no motor novo, e agora a restrição **nunca** convive com "liberada". |

## 3. O que foi feito

### 3.1 Motor de análise separado: `src/AmarV.Core`

É um projeto .NET 10 **sem dependência de MAUI nem de SQLite**. Ele pode ser testado no computador, e o app o usa por referência de projeto.

Para cada vacina, o motor:

1. **escolhe o esquema certo para a idade.** As regras são agrupadas em séries (rotina da criança, recuperação de 7 a 9 anos, adolescente, adulto, idoso, gestante, grupos especiais);
2. **conta as doses do histórico com equivalências.** Pentavalente conta como hepatite B e como dT; DTP e dTpa contam para o dT; VOP conta para a VIP; tetraviral conta para SCR e varicela. A tabela fica em `CatalogoVacinas.cs`, para o PO validar;
3. **calcula a próxima dose** com a data mínima (idade mínima + intervalo desde a última dose), a data recomendada e a data-limite (idade máxima);
4. **classifica a vacina** como `ATRASADA`, `APLICAR HOJE`, `AGUARDAR INTERVALO`, `FUTURA`, `EM DIA` ou `FORA DA FAIXA` (não recuperar);
5. **aplica as restrições** `CONTRAINDICADA`, `ADIAR` ou `AVALIAR`, em campo separado. Uma vacina com restrição **nunca** aparece como indicada para hoje.

A partir disso, o motor também:

- **monta o plano de recuperação em atendimentos com data**, respeitando os intervalos, a idade máxima, a regra das vivas injetáveis (mesmo dia ou 28 dias), a separação de FA e SCR em menores de 2 anos e o limite SC/IM por atendimento. Doses próximas (até 10 dias de diferença) são agrupadas no mesmo retorno para reduzir as idas à unidade;
- **define a situação geral**: `REGULAR`, `COM DOSES PARA APLICAR` ou `EM ATRASO`, mais o indicador "requer avaliação". Só o calendário do SUS conta; a rede privada aparece separada como "disponível";
- **trata as regras especiais**:
  - dTpa, VSR e COVID **por gestação**, conforme as semanas;
  - dTpa no puerpério;
  - doses anuais e semestrais;
  - reforço de dT a cada 10 anos;
  - febre amarela em dose única a partir de 5 anos;
  - Pneumo 10 em dose única a partir de 12 meses;
  - hepatite B com 16 semanas entre a 1ª e a 3ª dose;
  - regra sem intervalo cadastrado usa o intervalo da dose anterior (e avisa);
- **valida os dados**: falta de data de nascimento, dose com data futura, dose anterior ao nascimento, doses sem data.

Arquivos principais:

| Arquivo | Para que serve |
|---|---|
| `Analise/MotorAnaliseVacinal.cs` | Passo a passo da análise |
| `Analise/Esquemas.cs` | Agrupa as regras em séries e escolhe as séries do paciente |
| `Analise/PlanejadorRecuperacao.cs` | Monta os atendimentos com data |
| `Analise/RegrasSeguranca.cs` | Contraindicações, precauções e alertas |
| `Analise/CatalogoVacinas.cs` | Nomes equivalentes, vacinas vivas, vias e grupos especiais |
| `Analise/ParametrosAnalise.cs` | Tolerância de atraso (30 dias), horizonte (12 meses), limites SC/IM, retorno |
| `Relatorios/FormatadorTexto.cs` / `FormatadorHtml.cs` | Relatório individual (RF07) em texto e em HTML para imprimir ou salvar em PDF |
| `Relatorios/RelatorioConsolidado.cs` | RF20, RF21 e relatório geral (texto, HTML e CSV) |
| `Relatorios/ResumoParaBanco.cs` | Preenche as colunas da tabela de análise |
| `Relatorios/CalendarioResumo.cs` | Calendário montado das regras (RF05) |

### 3.2 Testes automatizados: `tests/AmarV.Core.Testes`

São **49 testes**, todos passando, com 10 pacientes fictícios (bebê sem vacina, criança com caderneta incompleta, criança em dia, adulta lactante, gestante de 29 semanas, idoso, adolescente em quimioterapia, criança com febre, profissional de saúde…). Cada teste descreve uma regra, por exemplo: "Febre amarela NÃO fica liberada para lactante de bebê < 6 meses" ou "Hepatite B: 3ª dose ≥ 16 semanas após a 1ª".

### 3.3 Demonstração: `tools/AmarV.Demonstracao`

O comando `dotnet run` analisa os 10 pacientes e gera, na pasta `saida/`:

- o relatório HTML de cada paciente;
- os relatórios RF20 e RF21 e o relatório geral, em HTML e CSV;
- o calendário montado das regras.

Esses arquivos servem como **evidência para a Agenda**.

### 3.4 Aplicativo MAUI: `MauiApp1` (revisado6 + correções + integração)

| Arquivo | Mudança |
|---|---|
| `App.xaml.cs`, `Helpers/UsuarioHelper.cs`, `Views/LoginPage.xaml.cs`, `Views/CadastroPessoa.xaml(.cs)` | Correções de login e cadastro do pacote de 26/10, aplicadas |
| `maui_amarv.csproj` | Removido o caminho `C:\Users\jesui\...`; adicionada a referência ao `AmarV.Core`; versão 2.1 (build 3) |
| `Resources/Fonts` | Fontes OpenSans restauradas |
| `Services/AnaliseEsquemaVacinal.cs` | Passou a ser a ponte entre o SQLite e o motor: `AnalisarAsync` (estruturado), `AnalisarTodosAsync` (relatórios), `SalvarAsync` (grava as colunas). `AnalisarPacienteAsync` continua existindo e devolve texto. O motor antigo foi guardado em `docs/legado/` |
| `Models/AnaliseEsquemaVacinal.cs` | Novas colunas com os **mesmos nomes do MariaDB**, que a sincronização já envia. O SQLite-net cria as colunas sozinho |
| `Views/AnaliseVacinalPage` | Tela nova com cartões coloridos por situação, alertas, plano de atendimentos com data, próximas doses e restrições. Tem o botão **"Registrar dose aplicada hoje"**, que grava no histórico e refaz a análise. O relatório HTML é compartilhado pelo menu do sistema (funciona no Android) |
| `Views/RelatorioPage.xaml.cs` | RF07, RF20, RF21 e geral usam a mesma análise. Corrigidos o seletor de paciente e o salvamento dentro do laço. CSV com acentos para o Excel e HTML para imprimir |
| `Views/CalendarioVacinal` | Lê as regras do banco, com filtros de calendário (SUS ou rede privada) e de grupo (RF05, RF11, RF12) |

As demais telas (cadastro, histórico, sincronização, backup, usuários) **não foram alteradas**.

## 4. Antes × depois (mesmos pacientes)

| Paciente | Motor antigo | Motor novo |
|---|---|---|
| Bebê de 2 meses sem vacinas | Tudo "LIBERADA"; "Nenhuma dose futura" | **EM ATRASO**. BCG atrasada; Penta, VIP, Pneumo e Rota para hoje; 11 atendimentos com data no próximo ano |
| Adulta de 35 anos, lactante | Hepatite B, dT e SCR "LIBERADA"; sem datas | **EM ATRASO**. Febre amarela **ADIAR** e fora do plano; 4 atendimentos (26/10, 23/11, 25/12, 23/02) |
| Idoso de 68 anos | FA "LIBERADA" junto com alerta de avaliação | dT (reforço) atrasado; FA **AVALIAR**; hepatite B em 3 atendimentos |
| Criança de 1 a 6 m com caderneta completa | Relatório antigo: pendente (bug do `CalcularPendencias`) | **REGULAR**; reforços dos 4 anos como próximos |

Saídas completas: `docs/comparacao/motor_antigo_revisado6.txt` e `docs/comparacao/motor_novo_mesmos_cenarios.txt`.

## 5. Decisões para o PO (Ludgerio) validar

Ficaram como **parâmetros** ou em **um único arquivo**, fáceis de mudar:

1. **Tolerância de atraso: 30 dias.** Até 30 dias depois da data recomendada a vacina aparece como "aplicar hoje"; depois disso, "atrasada". (`ParametrosAnalise.ToleranciaAtrasoDias`)
2. **Limite por atendimento: 2 SC, e 2 IM (≤ 24 meses) ou 4 IM.** Ver o item 2.6-g: a visita de 2 meses tem 3 IM. (`Services/AnaliseEsquemaVacinal.CriarParametros`)
3. **Equivalências entre vacinas** (pentavalente → hepatite B e dT, etc.) em `CatalogoVacinas.ContaComo`.
4. **Rede privada não define atraso.** Ela aparece como "DISPONÍVEL".
5. **Gestante:** vacinas vivas são contraindicadas e programadas para depois do parto; febre amarela é contraindicada, salvo risco epidemiológico; HPV é adiada.
6. **Fora da faixa:** só é listado para menores de 10 anos, para não poluir a análise de adultos.
7. **Corrigir as regras 250 e 260** (intervalo da 3ª dose de dT). Hoje o motor usa os 60 dias da 2ª dose e registra a observação.

## 6. Limitações e próximos passos

- **A camada MAUI não foi compilada aqui.** O ambiente onde trabalhei não tem o workload do MAUI. O motor, os testes e a demonstração foram compilados e executados. O serviço `AnaliseEsquemaVacinal.cs` foi compilado com os modelos reais do app e um banco simulado. As telas (`AnaliseVacinalPage`, `RelatorioPage` e `CalendarioVacinal`) foram escritas com a API do MAUI 10 e precisam ser compiladas no Visual Studio. O roteiro está no LEIA-ME.
- O servidor PHP não foi alterado. As colunas novas da análise já existem no MariaDB, e o `sincronizar.php` grava os campos pelo nome da coluna.
- O cadastro não tem campos para comorbidades, deficiência, quilombolas e privados de liberdade. Ficam como limitação, assim como já registrado em `docs/revisao_regras_norma_sp.md`.
- Sugestão para a Agenda: atualizar o Quadro 1 com RF05 → Implementado (calendário lido das regras), RF08, RF13 e RF14 → Implementado com testes automatizados, e RF20 e RF21 → corrigidos para usar a análise. Os HTMLs da pasta `saida/` podem ser usados como evidência.

> A análise é um apoio à decisão e não substitui a avaliação do profissional de saúde. As regras clínicas implementadas devem ser validadas pelo PO com a Norma Técnica antes do uso real.
