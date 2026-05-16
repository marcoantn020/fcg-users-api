# UsersAPI

API de Usuários para o ecossistema FIAPGame. Fornece autenticação, gestão de usuários e publicação de eventos de integração ao criar usuários, com segurança via JWT, persistência em PostgreSQL e mensageria via RabbitMQ com MassTransit e Outbox transacional.

## Tecnologias

- .NET 8, ASP.NET Core (Minimal APIs + Swagger)
- ASP.NET Core Identity (com Roles)
- Entity Framework Core (Npgsql)
- PostgreSQL
- MassTransit + RabbitMQ
- EF Outbox (garantia de publicação de eventos)
- JWT (Bearer)
- Health Checks

## Arquitetura/Componentes

- API Endpoints: definidos via extensão MapAuthEndpoints().
- Autenticação/Autorização: JWT Bearer com validação de emissor, audiência e chave simétrica.
- Identidade: Identity Core com AppUser, roles e token providers.
- Persistência: DbContext (UsersDbContext) com Npgsql.
- Mensageria: Publicação do evento UserCreatedEventV1 no exchange topic fcg.users via RabbitMQ, com MassTransit e EF Outbox (publicação confiável atômica com a transação do banco).
- Observabilidade: Logging (Console/Debug), Swagger UI, Health Check em /health.
- Seed de Roles: execução automática no startup.

## Configuração

Arquivo appsettings.json (exemplo dev):

- ConnectionStrings:Default
    - Host=localhost;Port=5432;Database=fcg_users_db;Username=users;Password=userspw
- Jwt
    - Issuer: fcg.users
    - Audience: fcg
    - Key: ALTERE para uma chave longa e segura (>= 32 chars)
- RabbitMq
    - Host: localhost
    - Username: guest
    - Password: guest
    - VirtualHost: /

Variáveis de ambiente podem sobrescrever as chaves acima conforme convenção ASP.NET Core.

## Pré-requisitos

- .NET SDK 8.0
- PostgreSQL acessível e com o banco configurado (conforme ConnectionStrings:Default)
- RabbitMQ acessível (conforme seção RabbitMq)

## Migrações do Banco

- Crie/aplique migrações normalmente com EF Core Tools:
    - dotnet ef migrations add Initial --project UsersAPI
    - dotnet ef database update

(Adapte o caminho do projeto se necessário.)

## Executando

- Restaurar e executar:
    - dotnet restore
    - dotnet run
- A API sobe com:
    - Swagger: /swagger
    - Health: /health
- Autenticação:
    - Bearer JWT (obtenha via endpoints de auth definidos por MapAuthEndpoints()).

## Segurança

- Em produção:
    - Exigir HTTPS.
    - Definir Jwt:Key segura via secrets/variáveis de ambiente.
    - Restringir AllowedHosts.
    - Usar credenciais seguras para DB e RabbitMQ.

## Publicação de Eventos

- Evento: UserCreatedEventV1 (Contracts.IntegrationEvents)
- Exchange: fcg.users (topic)
- Outbox: configurado com EntityFrameworkOutbox para publicação confiável (UsePostgres + BusOutbox).

## Health Checks

- Endpoint: GET /health
- Inclui verificação de conectividade ao UsersDbContext.

## Logs

- Console e Debug habilitados.
- Filtros:
    - MassTransit: Debug
    - EF Core e ASP.NET: Information

## Endpoints

- Swagger UI lista todos os endpoints.
- Autenticação e gestão de usuários expostos via MapAuthEndpoints().
- Protegidos por JWT conforme políticas.

## Seed de Roles

- Executado no startup via RoleSeeder.SeedAsync.
- Garante perfis/roles iniciais.

#### Por Marco Antonio