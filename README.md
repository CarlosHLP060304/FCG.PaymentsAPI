# FCG.PaymentsAPI

Microsserviço independente responsável exclusivamente pelo domínio financeiro da plataforma FCG. A implementação neste estágio estabelece a fundação técnica para evolução do processamento de pagamentos.

## Responsabilidades

- Consumir `OrderPlacedEvent` via RabbitMQ e criar pagamentos `Pending` de forma idempotente.
- Registrar e processar pagamentos, definir aprovação ou rejeição e garantir idempotência.
- Futuramente publicar `PaymentProcessedEvent`.

O serviço não é responsável por catálogo, pedidos, biblioteca, usuários ou notificações e não acessa bancos pertencentes a outros serviços. Os contratos de integração serão adicionados no card correspondente; entidades de domínio não serão usadas como contratos de mensagem.

## Tecnologia

- .NET 8 / ASP.NET Core
- Swagger/OpenAPI
- Health endpoint: `GET /health`
- Testes: xUnit
- Mensageria: MassTransit com RabbitMQ

## Estrutura

- `src/FCG.Payments.Api`: host HTTP, configuração, DI e endpoints operacionais.
- `src/FCG.Payments.Application`: casos de uso de criação idempotente e atualização de status, além das abstrações.
- `src/FCG.Payments.Domain`: pagamentos, tentativas e regras de transição do domínio financeiro, sem dependências de outras camadas.
- `src/FCG.Payments.Infrastructure`: persistência EF Core exclusiva do serviço e futuras integrações de mensageria.
- `tests/FCG.Payments.UnitTests`: testes unitários.
- `tests/FCG.Payments.IntegrationTests`: testes de integração do host.

Direção das dependências: Api → Application/Infrastructure; Infrastructure → Application/Domain; Application → Domain; Domain não depende dos demais projetos.

## Executar

Na raiz do repositório:

```sh
dotnet restore FCG.Payments.sln
dotnet run --project src/FCG.Payments.Api
```

Em Development, Swagger fica disponível em `/swagger`. O health endpoint fica em `/health`.

## Testar

```sh
dotnet test FCG.Payments.sln
```

## Configuração e segredos

A configuração padrão está em `appsettings.json` e `appsettings.Development.json`. Configure o banco PostgreSQL próprio do serviço por `ConnectionStrings__PaymentsDatabase` e o broker por `RabbitMq__Host`, `RabbitMq__Username` e `RabbitMq__Password` (use User Secrets/Secrets em ambientes reais). A fila consumida é `payments-order-placed`; falhas transitórias recebem três retries escalonados e, após esgotamento, o MassTransit move a mensagem para `payments-order-placed_error`. As migrations PostgreSQL pertencem ao projeto Infrastructure. A Inbox garante unicidade por consumidor e `EventId`; `OrderId` também é único. Pagamento e Inbox são confirmados na mesma transação. `CorrelationId` do evento é persistido no pagamento. O serviço não cria chaves estrangeiras para Order, User ou Game. Uma tentativa financeira é registrada em `PaymentAttempt` e permanece ligada ao pagamento lógico.

## Simulação de pagamento

Ao consumir `OrderPlacedEvent`, o PaymentsAPI cria/reutiliza o pagamento pendente e aplica uma regra determinística configurada por `PaymentSimulation:ApprovalLimit` (padrão `100.00`): valor menor ou igual ao limite é `Approved`; valor acima é `Rejected`. A decisão e uma `PaymentAttempt` são persistidas juntas. Estados terminais não são reprocessados e não geram nova tentativa. Ajuste `PaymentSimulation__ApprovalLimit` para demonstrações; por exemplo, um valor baixo testa rejeição e um valor abaixo do limite testa aprovação.

## Status da implementação

Fundação financeira e consumo idempotente implementados: pagamentos `Pending` por padrão, transições simuladas e determinísticas para `Approved`/`Rejected`, tentativas, Inbox persistente, unicidade por `OrderId`, consumer MassTransit/RabbitMQ e transações locais. A publicação de `PaymentProcessedEvent` permanece para o card seguinte.