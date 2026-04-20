Sistema de Monitoramento - Backend
Descrição

Este projeto consiste no desenvolvimento de um backend para um sistema de monitoramento de sistemas. O objetivo é coletar, processar e disponibilizar informações sobre o estado e desempenho de aplicações e serviços.

A aplicação foi desenvolvida utilizando C# com .NET, seguindo o padrão de APIs REST. A persistência de dados é feita com Entity Framework, garantindo integração eficiente com banco de dados relacional.

A arquitetura foi organizada em camadas, visando escalabilidade, manutenção e clareza no código.

Funcionalidades
Registro de eventos de monitoramento
Armazenamento de dados de sistemas
Consulta de informações via endpoints REST
Estrutura preparada para processamento assíncrono
Tecnologias Utilizadas
C#
.NET
Entity Framework
APIs REST
Banco de dados relacional
Estrutura do Projeto

O projeto segue uma arquitetura em camadas, separando responsabilidades como:

Controllers (camada de entrada da API)
Services (regras de negócio)
Repositories (acesso a dados)
Models/Entities (representação dos dados)

Como executar o projeto:

Pré-requisitos
.NET SDK instalado
Banco de dados relacional configurado (ex: SQL Server ou MySQL)
Passos
Clone o repositório:
git clone <url-do-repositorio>
Acesse a pasta do projeto:
cd nome-do-projeto
Configure a string de conexão no arquivo appsettings.json
Execute as migrations (caso utilize):
dotnet ef database update
Execute a aplicação:
dotnet run

Próximos Passos
Implementação de Worker Service para processamento em segundo plano
Coleta automática de métricas
Melhorias de performance e logs
Autor

Desenvolvido por Thialer como parte do aprendizado e evolução em desenvolvimento backend.
