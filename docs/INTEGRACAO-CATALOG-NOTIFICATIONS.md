# Integração de pagamentos com Catalog e Notifications

Correção validada localmente em 18/10/2026, com .NET 8 e MassTransit 8.3.6.

## O problema encontrado

O Catalog aceitava o pedido e gravava `OrderPlacedEvent` na sua Outbox. A publicação usava o exchange `OrderPlacedEvent`, mas o Payments estava inscrito em `FCG.Payments.Application.Messaging:OrderPlacedEvent`. São rotas diferentes. O broker devolvia a publicação obrigatória sem destino; a Outbox registrava `Unroutable` e mantinha o pedido em `PendingPayment`.

Além da rota, os serviços precisam combinar o formato. Catalog publica um objeto JSON direto, com `Content-Type: application/json`, propriedades PascalCase e sem envelope nem header `MT-MessageType`. O consumidor padrão MassTransit espera sua própria identificação de contrato.

Na saída existia outra diferença: o contrato interno do Payments e o contrato consumido pelo Notifications tinham namespaces diferentes, que geravam URNs distintos dentro do envelope MassTransit.

## Como a correção funciona

| Trecho | Comportamento aplicado |
|---|---|
| Catalog → Payments | `OrderPlacedEvent` é configurado no exchange curto com esse mesmo nome. |
| Entrada `payments-order-placed` | Aceita JSON direto usando `UseRawJsonDeserializer(RawSerializerOptions.All, isDefault: true)`. A opção fica restrita ao endpoint dedicado a esse único tipo; o validador de dados permanece ativo. |
| Processamento financeiro | Mantém as regras, transações, Inbox, tentativa única e Outbox existentes. Não houve alteração de schema. |
| Payments → Catalog/Notifications | Mantém o envelope JSON MassTransit e o exchange `PaymentProcessedEvent`. |
| Identidade do resultado | O DTO `PaymentProcessedIntegrationEvent`, na API, publica o URN esperado pelo Notifications atual. A Application continua sem referência ao MassTransit. |
| IDs do transporte | `MessageId` recebe o `EventId` persistido, e o envelope usa o mesmo `CorrelationId` do corpo. |

O URN transmitido é:

```text
urn:message:FCG.Notifications.Application.Messaging.Contracts:PaymentProcessedEvent
```

Na versão **8.3.6** utilizada aqui, o atributo recebe a parte sem o prefixo e a biblioteca acrescenta `urn:message:`:

```csharp
[MessageUrn("FCG.Notifications.Application.Messaging.Contracts:PaymentProcessedEvent")]
```

Esse é o identificador compatível com o contrato já presente no Notifications. Ele não depende do nome CLR do novo DTO da API. Uma futura padronização dos contratos do grupo precisa coordenar qualquer mudança desse identificador entre produtor e consumidores.

O DTO copia todos os campos do resultado financeiro. Não transforma status, IDs ou valores. A Outbox continua armazenando o evento da Application; a adaptação acontece somente ao publicar no transporte.

## O percurso de uma compra

1. O cliente obtém um token no Users e cria o pedido no Catalog.
2. Catalog grava pedido e Outbox na mesma transação. Seu worker tenta publicar o evento persistido.
3. Payments consome o JSON, registra a Inbox e cria ou recupera o pagamento por `OrderId`.
4. O simulador aprova valores até o limite configurado e rejeita valores acima. Persiste decisão, tentativa e Outbox de resultado.
5. O worker Payments publica `PaymentProcessedEvent`. Catalog e Notifications recebem cópias em filas independentes.
6. Catalog registra sua Inbox e aplica o resultado: `Paid` com aquisição na biblioteca, ou `Rejected` sem aquisição.
7. Notifications confirma a compra aprovada por envio simulado no console; no resultado rejeitado, registra que não gera confirmação.

Notifications e Catalog processam suas cópias independentemente. A confirmação financeira não garante que a biblioteca já tenha sido atualizada naquele instante; a validação completa consulta os dois efeitos.

## Arquivos alterados

- `Messaging/PaymentsMessagingExtensions.cs`, na API: nomes de exchanges e leitura de JSON na entrada.
- `Messaging/PaymentProcessedIntegrationEvent.cs`, na API: contrato de transporte e conversão a partir da Application.
- `Messaging/MassTransitPaymentProcessedEventPublisher.cs`, na API: adaptação e IDs do envelope.
- `Messaging/PaymentOutboxDispatcher.cs`, na Infrastructure: nome correto do exchange nos logs.
- `Dockerfile` e `.dockerignore`: imagem .NET 8, execução com usuário não root e exclusão de arquivos locais do contexto.
- `PaymentTransportCompatibilityTests.cs`: verifica a publicação real, serialização e consumo por outro tipo CLR com o contrato externo.

## Como validar

Na raiz do Payments:

```powershell
dotnet test FCG.Payments.sln --configuration Release
docker build -t fcg/payments-api:integracao-local .
```

Os testes de transporte executam Approved e Rejected em memória, com serialização JSON real, outro tipo de consumidor, enum e IDs. Conferem o envelope, URN, Content-Type e campos preservados. Eles não substituem o teste de bindings e persistência com RabbitMQ/PostgreSQL reais.

No ambiente integrado, configure:

| Variável | Valor/formato |
|---|---|
| `ConnectionStrings__PaymentsDatabase` | Conexão com o banco próprio, com migrations existentes já aplicadas. |
| `RabbitMq__Enabled` | `true` |
| `RabbitMq__Host` | URI completa, por exemplo `rabbitmq://rabbitmq`. |
| `RabbitMq__Username` / `RabbitMq__Password` | Credenciais do broker compartilhado. |
| `PaymentSimulation__ApprovalLimit` | `100.00` para os cenários de R$ 49,90 e R$ 149,90. |

O Dockerfile não cria bancos nem aplica migrations automaticamente. A preparação pertence ao ambiente integrado. Não coloque `localhost` para apontar a outro container.

## Evidência obtida

- 23 testes unitários e três testes de integração da solução aprovados.
- Os mesmos dois pedidos antes pendentes foram entregues automaticamente pela Outbox do Catalog, após ativar a imagem local corrigida.
- Pedido `6d6db0de-a5c3-438f-8f7a-031a32bb4d8e`: pagamento Approved de R$ 49,90, pedido Paid, uma aquisição e confirmação simulada.
- Pedido `adaf6389-d833-4db5-890a-0e772035831f`: pagamento Rejected de R$ 149,90, pedido Rejected, nenhuma aquisição e log explícito de não confirmação.
- 30 verificações integradas aprovadas, sem falhas ou bloqueios. Conferidos valores, usuário, jogo, correlação, tentativa única, Inboxs, Outboxes publicadas, API e logs Notifications.
- Repetir a entrada HTTP recuperou os mesmos pedidos. Esse teste não provocou redelivery de mensagens nem comprova a deduplicação desse redelivery.

A evidência integrada está no repositório FCG, em `docs/integracao/PLANO-E-RESULTADOS-INTEGRACAO.md`, seção 12. O JSON local da rodada é `TestResults/Integracao/Compras/b9e7bf863dc740cc89f364f4d4208929/resultado.json`; ele não faz parte do Git.

## Situação de publicação

A validação usou **`fcg/payments-api:integracao-local`**, construída nesta máquina. A tag antiga `fgcprojetofiap/payments-api:1.0` do Hub não recebe a correção por isso. Para distribuir ao grupo, é necessário revisar/versionar esta alteração no Payments, publicar uma nova tag e atualizar o Compose geral para essa tag. Nenhuma imagem foi enviada ao Hub por esta etapa.

Ainda são entregas próprias: teste provocado de redelivery, recuperação controlada de indisponibilidade, validação em ambiente limpo e implantação Kubernetes. O envio Notifications observado é simulado no console, sem SMTP real.
