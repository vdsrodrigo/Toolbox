# 📊 ToolBox - Utilidades para Processamento de Dados 🚀

## 📝 Descrição

O ToolBox é uma aplicação de console desenvolvida em .NET que oferece diversas ferramentas de processamento de dados. Atualmente, inclui funcionalidades para:

1. Importação em massa de dados de membros de um arquivo CSV para uma coleção "ledgers" no MongoDB
2. Formatação e extração de campos específicos de arquivos JSONL
3. Buscar e substituir texto facilmente em arquivos
4. Importação em massa de dados JSONL no Redis
5. Processamento de arquivos SQL e migração
6. Geração de instruções MongoDB
7. Migração de dados entre sistemas
8. Processamento de CPFs do CSV
9. Importação em massa de dados JSONL para tabela "member" no PostgreSQL

A ferramenta foi projetada com foco em performance, confiabilidade e escalabilidade, implementando estratégias como processamento em lotes (batch processing) e tratamento adequado de erros.

## 🏗️ Estrutura do Projeto

```
ToolBox/
├── Domain/
│   ├── Entities/
│   │   └── Ledger.cs                 # Entidade de domínio
│   └── Exceptions/
│       └── DomainException.cs        # Exceções de domínio
├── Models/
│   ├── CsvMember.cs                  # DTO para mapeamento do CSV
│   ├── JsonMember.cs                 # DTO para mapeamento do JSONL
│   └── ImportResult.cs               # Modelo para resultado da importação
├── Services/
│   ├── CsvImportService.cs           # Serviço de importação CSV
│   ├── CsvReaderService.cs           # Serviço de leitura do CSV
│   ├── JsonFormatterService.cs       # Serviço de formatação JSON
│   ├── JsonReaderService.cs          # Serviço de leitura JSONL
│   ├── JsonToPostgresService.cs      # Serviço de importação JSONL para PostgreSQL
│   ├── MongoDbService.cs             # Implementação do repositório MongoDB
│   ├── ConsoleService.cs             # Serviço de apresentação no console
│   ├── JsonToRedisService.cs         # Serviço de importação para o Redis
│   ├── TextReplacementService.cs     # Serviço de substituição de texto
│   └── ProgressBarService.cs         # Serviço centralizado de barras de progresso
├── Configuration/
│   ├── ApplicationSetup.cs           # Configuração da aplicação
│   ├── MongoDbSettings.cs            # Configurações do MongoDB
│   ├── RedisSettings.cs              # Configurações do Redis
│   └── PostgresSettings.cs           # Configurações do PostgreSQL
└── Program.cs                        # Ponto de entrada da aplicação
```

## ✨ Principais Funcionalidades

### Importação CSV para MongoDB

- 🔄 Importação em lotes (batch processing) para melhor performance
- 📈 Criação automática de índice único no campo CPF
- 📊 Relatório detalhado de estatísticas de importação
- 📈 Barra de progresso com estimativa de tempo restante

### Formatação de Arquivos JSONL

- 🔍 Extração de campos específicos de arquivos JSONL
- 📊 Barra de progresso com estimativa de tempo restante
- 📄 Geração de novo arquivo com prefixo "\_formatted"

### Buscar e substituir textos em arquivos

- 🔎 Busca rápida e eficiente de texto nos arquivos
- ✂️ Substituição automática do texto encontrado por palavras ou expressões definidas pelo usuário
- 🗑️ Suporte para remoção de texto (deixando o campo de substituição vazio)
- 📑 Geração automática de arquivo resultante com prefixo "\_replaced"
- ✅ Exibição resumida com o total de linhas processadas e correspondências encontradas
- ⏱️ Exibição do tempo de processamento detalhado
- 📊 Barra de progresso com estimativa de tempo restante

### Publicação de JSONL para Redis

- 📥 Leitura de arquivos JSONL com eficiência e robustez
- 🔑 Seleção dinâmica de campos JSON como chave e valor
- ⚡ Publicação direta dos pares chave-valor no Redis
- ✅ Informação detalhada sobre a quantidade de entradas publicadas
- ⏱️ Mensuração clara do tempo gasto no processamento
- 📊 Barra de progresso com estimativa de tempo restante

### Processamento de Arquivos SQL e Migração

- 📄 Remoção de campos específicos de instruções SQL
- 📄 Executa instruções SQL em arquivos
- 🛡️ Suporte para PostgreSQL
- 📝 Logs detalhados de execução
- 🔄 Processamento de arquivos de migração
- 🗑️ Geração de instruções DELETE para limpeza prévia
- 📊 Ordenação correta de inserções (transaction, accrual, redemption)
- 🔍 Filtragem por ledger_customer_id

### Geração de Instruções MongoDB

- 📄 Gera instruções MongoDB para atualização de pontos
- 📄 Atualiza as collections:
  - `ledgers`: campos `points` e `pointsBlocked`
  - `balances`: campos `points`, `pointsAvailable` e `pointsBlocked`
- 🛡️ Suporta execução automática após geração

### Processamento de CPFs do CSV

- 📄 Lê CPFs da primeira coluna do arquivo CSV
- 🔍 Busca os CPFs no arquivo JSONL
- 📄 Gera um novo arquivo JSONL contendo apenas os registros dos CPFs encontrados
- 📊 Mostra barra de progresso com:
  - Total de linhas processadas
  - Total de CPFs encontrados
  - Total de linhas inválidas

### Importação JSONL para PostgreSQL

- 📥 Leitura eficiente de arquivos JSONL com estrutura no estilo MongoDB
- 🔄 Conversão automática de IDs e timestamps do formato MongoDB para PostgreSQL
- 🛡️ Criação automática de tabela e índices no PostgreSQL se não existirem
- 📊 Importação em lotes (batch processing) para performance otimizada
- 📈 Barra de progresso com estimativa de tempo restante
- 📊 Relatório detalhado de estatísticas de importação
- 🔑 Geração automática de UUIDs v7 para o campo external_id
- 📄 O arquivo de saída é gerado na mesma pasta do arquivo JSONL com o sufixo "\_final"

## 🔍 Como Funciona

### Importação CSV para MongoDB

O sistema realiza a importação seguindo estas etapas:

1. **Configuração**: Carrega configurações via `ApplicationSetup`
2. **Preparação**: Cria índice único no CPF via `ILedgerRepository`
3. **Leitura CSV**: Processa o arquivo usando `ICsvReaderService`
4. **Mapeamento**: Converte registros CSV para entidades `Ledger`
5. **Processamento**: Insere lotes via `ILedgerRepository`
6. **Relatório**: Gera estatísticas via `ConsoleService`

### Formatação de Arquivos JSONL

O processo de formatação segue estas etapas:

1. **Entrada**: O usuário especifica o caminho do arquivo JSONL e os campos a extrair
2. **Preparação**: O sistema analisa o arquivo para determinar seu tamanho total
3. **Processamento**: Cada linha é lida, processada e os campos selecionados são extraídos
4. **Monitoramento**: Uma barra de progresso exibe o status, incluindo porcentagem concluída e tempo estimado restante
5. **Saída**: Um novo arquivo é criado com o sufixo "\_formatted", contendo apenas os campos selecionados

### Buscar e substituir textos em arquivos

A funcionalidade de busca e substituição atua nas seguintes etapas:

1. **Entrada**: Solicita o caminho do arquivo original ao usuário
2. **Parâmetros**: Solicita o texto que deve ser encontrado e a expressão que substituirá esse texto nas ocorrências (caso o campo de substituição seja deixado vazio, o texto será removido)
3. **Processamento**: Cada linha é analisada e processada rapidamente, realizando as substituições ou remoções necessárias
4. **Resultados**: Ao final, exibe um relatório contendo o total de correspondências encontradas, número total de linhas processadas, tempo consumido e o caminho do arquivo modificado gerado com prefixo "\_replaced"

### Publicação de JSONL para Redis

A funcionalidade de publicação JSONL no Redis atua nas seguintes etapas:

1. **Entrada**: Solicita o caminho do arquivo JSONL original ao usuário
2. **Parâmetros**: Solicita ao usuário os nomes dos campos JSON que serão utilizados como chave e valor no Redis
3. **Processamento**: Percorre cada linha no arquivo, extraindo os valores configurados; insere os valores extraídos diretamente no Redis
4. **Resultados**: Após a conclusão, exibe um relatório detalhado contendo o total de entradas publicadas, quantidade total de linhas processadas, tempo consumido e eventuais linhas ignoradas devido à falta dos campos especificados

### Processamento de Arquivos SQL e Migração

O processo de processamento de arquivos SQL e migração segue estas etapas:

1. **Entrada**: Solicita o caminho do arquivo SQL ao usuário
2. **Parâmetros**:
   - Para processamento SQL: escolha entre remover campos, executar instruções ou filtrar linhas
   - Para migração: escolha se deseja filtrar por ledger_customer_id
3. **Processamento**:
   - Para SQL: processa o arquivo removendo campos ou executando instruções
   - Para migração: gera instruções DELETE e ordena inserções corretamente
4. **Resultados**: Exibe um relatório detalhado do processamento

### Geração de Instruções MongoDB

O processo de geração de instruções MongoDB segue estas etapas:

1. **Entrada**: Solicita o caminho do arquivo de migração SQL ao usuário
2. **Processamento**: Processa o arquivo, removendo campos específicos (item_number, legacy_redemption_id)
3. **Resultados**: Gera instruções MongoDB para atualização de pontos
4. **Atualização**: Atualiza as collections:

- `ledgers`: campos `points` e `pointsBlocked`
- `balances`: campos `points`, `pointsAvailable` e `pointsBlocked`

5. **Suporte**: Suporta execução automática após geração

### Processamento de CPFs do CSV

O processo de processamento de CPFs do CSV segue estas etapas:

1. **Entrada**: Solicita o caminho do arquivo CSV com os CPFs e o caminho do arquivo JSONL com os dados
2. **Processamento**: Lê CPFs da primeira coluna do arquivo CSV, busca os CPFs no arquivo JSONL e gera um novo arquivo JSONL contendo apenas os registros dos CPFs encontrados
3. **Resultados**: Exibe um relatório detalhado do processamento, incluindo o total de linhas processadas, CPFs encontrados e linhas inválidas

## 📋 Modelos e Entidades

### Ledger (Entidade de Domínio)

```csharp
public class Ledger
{
    public string Cpf { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int LedgerTypeId { get; private set; }
    public int? Points { get; private set; }
    public int PointsBlocked { get; private set; }
    public string Status { get; private set; }
}
```

### CsvMember (DTO)

```csharp
public class CsvMember
{
    public string LoyMemberId { get; set; }
    public string MemberPeoMemNum { get; set; }
}
```

### ImportResult (Modelo)

```csharp
public class ImportResult
{
    public long TotalRecords { get; set; }
    public long InsertedRecords { get; set; }
    public long TotalBatches { get; set; }
    public long FailedBatches { get; set; }
    public double DurationInSeconds { get; set; }
    public double RecordsPerSecond { get; set; }
}
```

## ⚙️ Configuração

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "plis-core",
    "CollectionName": "ledgers"
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "plis-core"
  },
  "Postgres": {
    "ConnectionString": "Host=localhost;Port=5432;Database=plis-core;Username=postgres;Password=postgres"
  },
  "BatchSize": 1000,
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/importer-.log",
          "rollingInterval": "Day"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

## 🔍 Como Usar

Para aproveitar todos os recursos do **ToolBox**, siga as instruções abaixo para cada funcionalidade disponível mediante seleção no menu:

### 🚀 **1. Importação CSV para MongoDB**

- Execute o ToolBox, digite `1` e pressione `Enter`
- Informe o caminho completo para o arquivo CSV
- O sistema irá processar automaticamente o arquivo, mostrando o progresso e exibindo um relatório ao final

### 🛠️ **2. Formatação de Arquivos JSONL**

- Execute o ToolBox, digite `2` e pressione `Enter`
- Informe o caminho completo para o arquivo JSONL
- Informe quais campos deseja extrair
- Aguarde a formatação enquanto a barra de progresso é exibida
- Ao concluir, o arquivo formatado com os campos escolhidos será gerado automaticamente com o prefixo `_formatted`

### 🔄 **3. Buscar e Substituir Textos em Arquivos**

- Execute o ToolBox, digite `3` e pressione `Enter`
- Informe o caminho completo até o arquivo que pretende processar
- Digite o texto que você deseja buscar entre as linhas do arquivo
- Informe o novo texto que substituirá o encontrado:
  - **Para substituir**: digite o novo texto e pressione `Enter`
  - **Para remover o texto**: apenas pressione `Enter` e deixe o campo em branco
- O processo iniciará imediatamente e percorrerá o arquivo, exibindo as linhas processadas, o total de correspondências encontradas e o tempo gasto
- Ao terminar, será exibido um resumo completo com a localização do arquivo de saída gerado com sufixo `_replaced`

### 🚀 **4. Publicação de dados JSONL no Redis**

- Execute o ToolBox, digite `4` e pressione `Enter`
- Informe o caminho do arquivo JSONL
- Informe o campo JSON a ser usado como Chave
- Informe o campo JSON a ser usado como Valor
- O processamento iniciará imediatamente, lê cada linha e publica as entradas no Redis
- Exibe relatório com total de entradas publicadas e o tempo consumido ao concluir

### 📄 **5. Processar Arquivo SQL e Migração**

- Execute o ToolBox, digite `5` e pressione `Enter`
- Escolha entre:
  - Remover campos específicos
  - Executar instruções SQL
  - Filtrar linhas
  - Processar arquivo de migração
- Para migração:
  - Informe o caminho do arquivo SQL
  - Escolha se deseja filtrar por ledger_customer_id
  - Gere o arquivo formatado com deleções e inserções ordenadas

### 🚀 **6. Geração de Instruções MongoDB**

- Execute o ToolBox, digite `6` e pressione `Enter`
- Informe o caminho do arquivo de migração SQL
- O processamento iniciará imediatamente, processando o arquivo e gerando instruções MongoDB
- Ao concluir, verifique os arquivos gerados:
  - Arquivo SQL formatado
  - Instruções MongoDB para atualização de pontos

### 🚀 **7. Processar CPFs do CSV**

- Execute o ToolBox, digite `7` e pressione `Enter`
- Informe o caminho do arquivo CSV com os CPFs
- Informe o caminho do arquivo JSONL com os dados
- O processamento iniciará imediatamente, lê CPFs da primeira coluna do CSV, busca os CPFs no JSONL e gera um novo arquivo JSONL contendo apenas os registros dos CPFs encontrados
- Exibe relatório detalhado do processamento, incluindo o total de linhas processadas, CPFs encontrados e linhas inválidas

💻 **Exemplo prático:**

```console
Escolha uma opção:
1 - Importar CSV para MongoDB
2 - Formatar arquivo JSON
3 - Substituir Texto em Arquivo
4 - Ler JSONL e publicar dados no Redis
5 - Processar Arquivo SQL e Migração
6 - Geração de Instruções MongoDB
7 - Processar CPFs do CSV
0 - Sair
> 7

Informe o caminho do arquivo CSV com os CPFs:
> C:\dados\cpfs.csv

Informe o caminho do arquivo JSONL com os dados:
> C:\dados\dados.jsonl

Processando CPFs do CSV...
[██████████████████████████████████████████████████] 100%
CPFs processados com sucesso!
Arquivo de saída: C:\dados\dados_final.jsonl

Relatório de processamento:
- Total de linhas processadas: 1000
- Total de CPFs encontrados: 800
- Total de linhas inválidas: 200
```

## 📈 Design e Arquitetura

A aplicação segue princípios modernos de design:

- 🎯 **Domain-Driven Design (DDD)**

  - Entidades ricas com comportamento encapsulado
  - Exceções de domínio personalizadas

- 🔄 **Padrões de Design**
  - Injeção de Dependência
  - Repository Pattern
  - Service Pattern
  - Princípios SOLID
  - Progress Bar Service Pattern (centralização da lógica de barras de progresso)

## 🔍 Requisitos

- .NET 9.0 ou superior
- MongoDB
- Redis
- PostgreSQL (opcional, para processamento de SQL)

## 📋 Contribuição

1. Fork o projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request
