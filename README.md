# GuiaFoco: site + API (C# / ASP.NET Core 8 + MySQL)

Este pacote tem o **site do GuiaFoco já ligado à API**. Os dados do painel, do cadastro e dos desafios
agora vêm do banco MySQL, e não mais do navegador.

```
GuiaFoco/
├── docker-compose.yml     → liga o MySQL pelo Docker (opcional)
├── database/schema.sql    → script do banco (opcional: a API cria as tabelas sozinha)
└── GuiaFoco.Api/          → a API em C#
    └── wwwroot/           → o SITE (index.html, css, js). A API abre ele em http://localhost:5080
```

---

## Como rodar no Windows (passo a passo)

### 1. Ter o .NET instalado
Abra o `cmd` e digite `dotnet --list-sdks`.
- O recomendado é ter uma linha começando com **8.** Se não tiver, instale o **.NET 8 SDK**:
  https://dotnet.microsoft.com/download/dotnet/8.0 → coluna **SDK** → **Windows x64**.
- Se só aparecer o **10.**, o projeto também deve funcionar, porque ele foi configurado para
  rodar no .NET mais novo quando o 8 não estiver instalado.

### 2. Ligar o MySQL (escolha UMA opção)

**Opção A: com Docker** (o Docker Desktop precisa estar aberto e mostrando *Engine running*)
```
cd "C:\Users\camil\Downloads\GuiaFoco Claude\GuiaFoco"
docker compose up -d
```

**Opção B: sem Docker, com o MySQL instalado no Windows**
1. Instale o **MySQL Community Server** (versão 8.4 LTS, instalador *MSI* para Windows):
   https://dev.mysql.com/downloads/mysql/. Durante a instalação, anote a senha do usuário `root`.
   Instale também o **MySQL Workbench**: https://dev.mysql.com/downloads/workbench/
2. Abra o MySQL Workbench, conecte com o `root` e rode:
   ```sql
   CREATE DATABASE guiafoco CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
   CREATE USER 'guiafoco'@'localhost' IDENTIFIED BY 'guiafoco123';
   GRANT ALL PRIVILEGES ON guiafoco.* TO 'guiafoco'@'localhost';
   FLUSH PRIVILEGES;
   ```
   (Pronto: o usuário e a senha já são os mesmos que estão no `appsettings.json`.)

### 3. Rodar a API
```
cd GuiaFoco.Api
dotnet run
```
Espere aparecer `Now listening on: http://localhost:5080`. **Deixe essa janela aberta.**
Na primeira vez, a API cria as tabelas, carrega as perguntas e cria a conta de demonstração.

### 4. Abrir o site
Abra no navegador: **http://localhost:5080**

| Para testar | E-mail | Senha |
|---|---|---|
| Botão "Entrar na demonstração" | — | — |
| Login normal | demo@guiafoco.com | Demo@1234 |

Também dá para criar uma conta nova em **Criar uma conta**.

Outras páginas úteis:
- http://localhost:5080/swagger → lista e teste de todas as rotas da API
- http://localhost:5080/health → deve responder `{"status":"ok"...}`

Para desligar a API, volte na janela preta e aperte **Ctrl + C**.

---

## O que mudou no site
- **Login, cadastro e demonstração** usam a API (senha conferida no banco, com token JWT).
- **Início e Desempenho**: os números são calculados pela API a partir das sessões e respostas gravadas.
- **Tempo de tela**: o uso do dia e o relógio da sessão são reais. As opções de intervenção são salvas no banco.
- **Aviso "Hora de uma pausa!"**: aparece sozinho quando a sessão passa do tempo configurado, quando o
  limite diário acaba ou quando o uso está **fora do horário permitido**. Ele só abre como janela se
  "Bloqueio/interrupção" estiver ligado. Se estiver desligado, aparece só um aviso no canto.
- **Microdesafios**: 5 perguntas sorteadas do banco. Cada resposta é gravada (acerto, tentativa,
  tempo de resposta) e soma pontos.
- **Acessibilidade** (modo escuro, alto contraste, fonte) fica salva na conta.

Arquivos do site: `GuiaFoco.Api/wwwroot/index.html`, `css/style.css`, `js/app.js`,
`js/guiafoco-api.js` (conversa com a API) e `js/config.js` (endereço da API).

### E o site no GitHub Pages?
Para atualizar o repositório `GuiaFoco3`, copie o conteúdo de `wwwroot` para ele.
Mas o site publicado no GitHub Pages só vai funcionar quando a API também estiver publicada na
internet com **HTTPS**. Aí basta trocar o endereço em `js/config.js`. Enquanto isso, use
**http://localhost:5080**.

---

## Problemas comuns
| O que aparece | O que fazer |
|---|---|
| `Unable to connect to any of the specified MySQL hosts` / `Banco indisponível (tentativa X/10)` | O MySQL não está ligado. Faça o passo 2. |
| `Access denied for user 'guiafoco'` | Usuário ou senha do banco diferentes. Confira o passo 2 (Opção B) ou ajuste `ConnectionStrings:Default` no `appsettings.json`. |
| Site mostra "Não foi possível conectar à API" | A janela do `dotnet run` foi fechada. Rode de novo. |
| `Port 3306 is already in use` (Docker) | Já existe um MySQL instalado no Windows. Use a Opção B. |
| `address already in use` na porta 5080 | A API já está rodando em outra janela. |

## Configurações (`GuiaFoco.Api/appsettings.json`)
| Chave | O que faz |
|---|---|
| `ConnectionStrings:Default` | Conexão com o MySQL |
| `Jwt:Key` | Chave do login. Para publicar, troque por uma chave secreta sua (32+ caracteres). |
| `Cors:Origins` | Sites que podem chamar a API de fora (já inclui o GitHub Pages) |
| `App:TimeZone` | Fuso usado para "hoje" e horário permitido (`America/Manaus`) |
| `App:EnableDemo` | Liga a conta e o botão de demonstração |

## Rotas da API
| Tela | Rota |
|---|---|
| Entrar / Criar conta / Demonstração | `POST /api/auth/login` · `POST /api/auth/register` · `POST /api/auth/demo` |
| Restaurar login ao recarregar | `GET /api/auth/me` |
| Início | `GET /api/criancas/{id}/resumo` |
| Criança | `GET/PUT /api/criancas/{id}` |
| Desempenho | `GET /api/criancas/{id}/desempenho` · `GET /api/criancas/{id}/recomendacao` |
| Tempo de tela | `GET /api/criancas/{id}/uso-hoje` · `PUT /api/criancas/{id}/configuracoes` |
| Sessões | `POST /api/sessoes` · `PATCH /api/sessoes/{id}/encerrar` · `POST /api/sessoes/{id}/intervencao` |
| Desafios | `GET /api/desafios` · `GET /api/desafios/{id}/perguntas` · `POST /api/respostas` |

Senhas são guardadas com BCrypt, cada rota confere se a criança pertence ao responsável logado e
a resposta certa nunca é enviada ao site antes de a criança responder.
