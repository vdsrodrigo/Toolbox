# 📊 ToolBox - Utilidades para Processamento de Dados 🚀

## 📝 Descrição

O ToolBox é uma aplicação de console desenvolvida em .NET que oferece diversas ferramentas de processamento de dados. Atualmente, inclui funcionalidades para:

1. Importação em massa de dados de membros de um arquivo CSV para uma coleção "ledgers" no MongoDB
2. Formatação e extração de campos específicos de arquivos JSONL

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
│   └── ImportResult.cs               # Modelo para resultado da importação
├── Services/
│   ├── CsvImportService.cs           # Serviço de importação CSV
│   ├── CsvReaderService.cs           # Serviço de leitura do CSV
│   ├── JsonFormatterService.cs       # Serviço de formatação JSON
│   ├── MongoDbService.cs             # Implementação do repositório MongoDB
│   └── ConsoleService.cs             # Serviço de apresentação no console
├── Configuration/
│   ├── ApplicationSetup.cs           # Configuração da aplicação
│   └── MongoDbSettings.cs            # Configurações do MongoDB
└── Program.cs                        # Ponto de entrada da aplicação
```

## ✨ Principais Funcionalidades

### Importação CSV para MongoDB
- 🔄 Importação em lotes (batch processing) para melhor performance
- 📈 Criação automática de índice único no campo CPF
- 📊 Relatório detalhado de estatísticas de importação

### Formatação de Arquivos JSONL
- 🔍 Extração de campos específicos de arquivos JSONL
- 📊 Barra de progresso com estimativa de tempo restante
- 📄 Geração de novo arquivo com prefixo "_novo"

### Recursos Gerais
- 📝 Logging estruturado com Serilog
- ⚙️ Configuração flexível via appsettings.json
- 🛡️ Tratamento robusto de erros e exceções
- 🎯 Design orientado a domínio (DDD)
- 🔌 Arquitetura modular e extensível

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
5. **Saída**: Um novo arquivo é criado com o sufixo "_novo", contendo apenas os campos selecionados

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
public record ImportResult
{
    public long TotalRecords { get; set; }
    public long InsertedRecords { get; set; }
    public long TotalBatches { get; set; }
    public long FailedBatches { get; set; }
    public double DurationMs { get; set; }
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
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  }
}
```

## 🚀 Como Usar

### Menu Principal
Ao iniciar a aplicação, um menu interativo será exibido com as seguintes opções:

### Importação CSV para MongoDB
Selecione a opção 1 e siga as instruções para especificar o caminho do arquivo CSV.

### Formatação de Arquivo JSONL
Selecione a opção 2 e siga as instruções para:
1. Especificar o caminho do arquivo JSONL
2. Informar o nome do primeiro campo a extrair
3. Informar o nome do segundo campo a extrair

Uma barra de progresso será exibida mostrando o status da operação e o tempo estimado para conclusão. Ao finalizar, o caminho do novo arquivo será exibido.

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