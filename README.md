# 📊 ToolBox - CSV to MongoDB Ledger Importer 🚀

## 📝 Descrição

O ToolBox é uma aplicação de console desenvolvida em .NET que facilita a importação em massa de dados de membros de um arquivo CSV para uma coleção "ledgers" no MongoDB. A ferramenta foi projetada com foco em performance, confiabilidade e escalabilidade, implementando estratégias como processamento em lotes (batch processing) e tratamento adequado de erros.

## 🏗️ Estrutura do Projeto

```
ToolBox/
├── Domain/
│   ├── Entities/
│   │   └── Ledger.cs         # Entidade de domínio
│   └── Exceptions/
│       └── DomainException.cs # Exceções de domínio
├── Models/
│   ├── CsvMember.cs          # DTO para mapeamento do CSV
│   └── ImportResult.cs       # Modelo para resultado da importação
├── Services/
│   ├── CsvImportService.cs   # Serviço principal de importação
│   ├── CsvReaderService.cs   # Serviço de leitura do CSV
│   ├── MongoDbService.cs     # Implementação do repositório MongoDB
│   └── ConsoleService.cs     # Serviço de apresentação no console
├── Configuration/
│   ├── ApplicationSetup.cs   # Configuração da aplicação
│   └── MongoDbSettings.cs    # Configurações do MongoDB
└── Program.cs                # Ponto de entrada da aplicação
```

## ✨ Principais Funcionalidades

- 🔄 Importação em lotes (batch processing) para melhor performance
- 📈 Criação automática de índice único no campo CPF
- 📊 Relatório detalhado de estatísticas de importação
- 📝 Logging estruturado com Serilog
- ⚙️ Configuração flexível via appsettings.json
- 🛡️ Tratamento robusto de erros e exceções
- 🎯 Design orientado a domínio (DDD)
- 🔌 Arquitetura modular e extensível

## 🔍 Como Funciona

O sistema realiza a importação seguindo estas etapas:

1. **Configuração**: Carrega configurações via `ApplicationSetup`
2. **Preparação**: Cria índice único no CPF via `ILedgerRepository`
3. **Leitura CSV**: Processa o arquivo usando `ICsvReaderService`
4. **Mapeamento**: Converte registros CSV para entidades `Ledger`
5. **Processamento**: Insere lotes via `ILedgerRepository`
6. **Relatório**: Gera estatísticas via `ConsoleService`

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

### Uso Padrão
```bash
dotnet run
```
Importa o arquivo padrão `members_without_ledger.csv` da pasta da aplicação.

### Especificando Arquivo CSV
```bash
dotnet run -- /caminho/para/seu/arquivo.csv
```

## 📈 Design e Arquitetura

A aplicação segue princípios modernos de design:

- 🎯 **Domain-Driven Design (DDD)**
  - Entidades ricas com comportamento encapsulado
  - Exceções de domínio personalizadas
  - Separação clara entre domínio e infraestrutura

- 🔌 **SOLID**
  - Single Responsibility Principle (classes coesas)
  - Open/Closed Principle (interfaces extensíveis)
  - Liskov Substitution (implementações intercambiáveis)
  - Interface Segregation (interfaces específicas)
  - Dependency Inversion (inversão de controle)

- 🏗️ **Clean Architecture**
  - Separação em camadas
  - Dependências apontando para dentro
  - Domínio independente de infraestrutura

## 📊 Performance

- ⚡ Processamento em lotes configurável
- 🔍 Indexação otimizada
- 🧵 Operações assíncronas
- 📊 Métricas detalhadas de performance

## 📝 Logs e Monitoramento

Sistema de logging estruturado com Serilog:
- 📄 Logs em arquivo com rotação
- 🖥️ Logs em console
- 🔍 Contexto enriquecido

## 🔧 Tratamento de Erros

- 🛡️ Exceções de domínio personalizadas
- 📊 Contabilização de sucessos/falhas
- 🔄 Resiliência a falhas parciais

## 📚 Tecnologias Utilizadas

- **.NET**: Framework base
- **MongoDB.Driver**: Acesso ao MongoDB
- **CsvHelper**: Processamento CSV
- **Serilog**: Logging estruturado
- **Microsoft.Extensions.DependencyInjection**: IoC
- **Microsoft.Extensions.Configuration**: Configurações
