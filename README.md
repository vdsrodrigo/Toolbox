# 📊 ToolBox - CSV to MongoDB Ledger Importer 🚀

## 📝 Descrição

O ToolBox é uma aplicação de console desenvolvida em .NET que facilita a importação em massa de dados de membros de um arquivo CSV para uma coleção "ledgers" no MongoDB. A ferramenta foi projetada com foco em performance, confiabilidade e escalabilidade, implementando estratégias como processamento em lotes (batch processing) e tratamento adequado de erros.

## 🏗️ Estrutura do Projeto

```
ToolBox/
├── Models/
│   └── Ledger.cs              # Modelos de dados para MongoDB e CSV
├── Services/
│   ├── CsvImportService.cs    # Serviço para leitura e processamento do CSV
│   └── MongoDbService.cs      # Serviço para operações com MongoDB
├── Program.cs                 # Ponto de entrada da aplicação
└── appsettings.json           # Configurações da aplicação
```

## ✨ Principais Funcionalidades

- 🔄 Importação em lotes (batch processing) para melhor performance
- 📈 Criação automática de índice único no campo CPF para evitar duplicações
- 📊 Relatório detalhado de estatísticas após a importação
- 📝 Log completo de operações usando Serilog
- ⚙️ Configuração flexível via arquivo appsettings.json
- 🛡️ Tratamento robusto de erros e exceções

## 🔍 Como Funciona

O sistema realiza a importação seguindo estas etapas:

1. **Configuração**: Carrega as configurações do MongoDB e do processo de importação
2. **Preparação**: Cria um índice único no campo CPF se ele não existir
3. **Leitura CSV**: Processa o arquivo CSV linha por linha usando CsvHelper
4. **Mapeamento**: Converte cada registro do CSV para o modelo Ledger
5. **Processamento em Lotes**: Insere os registros no MongoDB em lotes de tamanho configurável
6. **Relatório**: Gera estatísticas detalhadas do processo de importação

## 📋 Modelo de Dados 

### Ledger (MongoDB)
- `Id`: ObjectId gerado pelo MongoDB
- `Cpf`: Número de identificação do membro (campo com índice único)
- `CreatedAt`: Data de criação do registro
- `LedgerTypeId`: Identificador do tipo de ledger (fixo como 1)
- `Points`: Pontos acumulados (pode ser nulo)
- `PointsBlocked`: Pontos bloqueados (padrão 0)
- `Status`: Status do ledger (padrão "Ativo")

### CsvMember (Entrada)
- `LOYMEMBERID`: ID do membro no sistema de lealdade
- `MEMBERPEOMEMNUM`: Número de CPF do membro

## ⚙️ Configuração

As configurações são armazenadas no arquivo `appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "plis-core",
    "CollectionName": "ledgers"
  },
  "ImportSettings": {
    "BatchSize": 5000
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
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
Isso importará o arquivo padrão `members_without_ledger.csv` localizado na pasta da aplicação.

### Especificando Arquivo CSV
```bash
dotnet run -- /caminho/para/seu/arquivo.csv
```

## 📈 Performance e Escalabilidade

A aplicação foi projetada pensando em performance:

- ⚡ Processamento em lotes de 5000 registros (configurável)
- 🔍 Indexação para evitar duplicatas e melhorar performance de escrita
- 🧵 Operações assíncronas para melhor utilização de recursos
- 📊 Medição de tempo e taxa de importação (registros por segundo)

## 📝 Logs e Monitoramento

O sistema utiliza Serilog para registrar logs detalhados:

- 📄 Logs em arquivo com rotação diária
- 🖥️ Logs no console para acompanhamento em tempo real
- 🔍 Informações enriquecidas com contexto, nome da máquina e ID da thread

## 🔧 Tratamento de Erros

A aplicação implementa tratamento robusto de erros:

- 🛡️ Detecção e relatório de erro nos lotes
- 📊 Contagem de registros com sucesso mesmo em caso de falhas parciais
- 🔄 Processamento continua mesmo quando um lote falha

## 📋 Resultado da Importação

Ao final do processo, o sistema exibe estatísticas detalhadas:

- Total de registros processados
- Total de registros importados com sucesso
- Número total de lotes
- Número de lotes com falha
- Duração total da operação
- Taxa média de importação (registros por segundo)

---

## 📚 Tecnologias Utilizadas

- **.NET Core**: Framework para a aplicação
- **MongoDB.Driver**: Biblioteca oficial para integração com MongoDB
- **CsvHelper**: Biblioteca para processamento de arquivos CSV
- **Serilog**: Framework de logging estruturado
- **Microsoft.Extensions.DependencyInjection**: Injeção de dependências
- **Microsoft.Extensions.Configuration**: Gerenciamento de configurações
