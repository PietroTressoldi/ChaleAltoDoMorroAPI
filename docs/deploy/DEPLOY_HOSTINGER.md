# Deploy na Hostinger + Cloudflare — passo a passo

A API e o banco ficam numa **VPS KVM da Hostinger**. A landing (já no ar) fica no **Cloudflare Workers** e o portal no **Cloudflare Pages**, os dois grátis.
Siga **na ordem**. Cada passo termina com um **✅ Confere**: só avance se ele deu certo.

> Troque `seudominio.com.br` pelo domínio real em **todos** os lugares.
> Todas as variáveis estão explicadas em [`docs/deploy/.env.example`](.env.example).
> O plano B (tudo na AWS) está em [`DEPLOY_AWS.md`](DEPLOY_AWS.md).

---

## 0. Como tudo vai ficar

```
                       ┌──────── VPS Hostinger (uma máquina Linux) ──────┐
 api.seudominio  ───►  │  Caddy (HTTPS)  ──►  API .NET  ──►  Postgres    │
                       │  portas 80/443        :8080          (fechado)  │
                       │                       fotos no disco (volume)   │
                       └─────────────────────────────────────────────────┘

 seudominio        ───►  Cloudflare Workers (build da landing, feito pela Cloudflare a cada push)
 portal.seudominio ───►  Cloudflare Pages  (build do portal, feito pela Cloudflare a cada push)
```

- O **Caddy** é um servidor web que tira o certificado HTTPS sozinho (Let's Encrypt) e repassa as requisições para a API.
- O **Cloudflare Pages** baixa o código do GitHub, roda o `npm run build` e publica o site com HTTPS. Quando o projeto
  **não tem `404.html`**, ele entende que é um app React (SPA) e devolve o `index.html` em qualquer rota. Por isso
  abrir `/reservas` direto funciona sem configurar nada.
- O **Postgres não fica aberto** para a internet. Só a API fala com ele, por dentro do Docker.
- O **DNS** fica na **Cloudflare**, que é grátis. O Pages exige isso para usar o domínio puro (`seudominio.com.br`).

Custo aproximado: KVM 1 (1 vCPU, 4 GB de RAM, 50 GB de disco) por ≈ R$ 30–50/mês no plano de 12 ou 24 meses
(a renovação sai mais cara, confira antes). Cloudflare: R$ 0.

---

## 1. Antes de começar (checklist)

- [ ] Domínio comprado (Registro.br ou outro) e acesso ao painel dele.
- [ ] Conta na **Cloudflare** (grátis) e na **Hostinger**, as duas com **2 fatores** ligado.
- [ ] Os repositórios da **landing** e do **portal** no GitHub (já estão: `chale-alto-do-morro-landing` e
      `ChaleAltoDoMorroGestao`).
- [ ] Um gerenciador de senhas aberto (Bitwarden, 1Password...) com um item **"Chalé — produção"**.
- [ ] No PC: `git` e `dotnet` (o `ssh` já vem no Windows 10/11).
- [ ] Tudo testado e commitado no `develop`, e o merge `develop` → `master` feito. O servidor baixa a `master`.

---

## 2. Passar o DNS do domínio para a Cloudflare (faça primeiro, ele demora)

1. Cloudflare → **Add a domain** → digite `seudominio.com.br` → plano **Free**.
2. Ela lê os registros que já existem. Confira e continue.
3. No fim, ela mostra **2 nameservers** (ex.: `ana.ns.cloudflare.com` e `bob.ns.cloudflare.com`).
4. No **Registro.br** (ou onde o domínio foi comprado): domínio → **DNS** → **Alterar servidores DNS** → cole os 2
   nameservers → salvar.

✅ **Confere** (pode levar de alguns minutos a algumas horas): o domínio aparece como **Active** no painel da Cloudflare.

---

## 3. Criar a VPS

### 3.1 Chave SSH (no **PC**)
No PowerShell:
```powershell
ssh-keygen -t ed25519 -C "chale-vps" -f $HOME\.ssh\chale-vps
Get-Content $HOME\.ssh\chale-vps.pub
```
Escolha uma senha para a chave quando ele pedir. Copie a linha que aparecer, começando em `ssh-ed25519 ...`.
Guarde os arquivos `chale-vps` e `chale-vps.pub` (pasta `C:\Users\<você>\.ssh`) no gerenciador de senhas.

### 3.2 Contratar e instalar
Hostinger → **VPS** → **KVM 1** → localização **Brasil (São Paulo)**. Na configuração:

| Campo | O que colocar |
|---|---|
| Sistema operacional | **Ubuntu 24.04** puro (sem painel e sem aplicativo) |
| Senha do root | uma senha forte. Guarde no gerenciador, só vai ser usada em emergência |
| Chave SSH | cole a linha do `chale-vps.pub` |

Quando terminar, anote o **IP** da VPS (aparece no painel, ex.: `191.101.10.20`). Na Hostinger o IP é fixo.

### 3.3 Firewall da Hostinger
hPanel → VPS → **Segurança** → **Firewall** → crie uma regra para cada porta e ative o firewall na VPS:

| Ação | Protocolo | Porta | Origem |
|---|---|---|---|
| Aceitar | TCP | 22 (SSH) | qualquer |
| Aceitar | TCP | 80 (HTTP) | qualquer |
| Aceitar | TCP | 443 (HTTPS) | qualquer |

Tudo que não estiver na lista fica bloqueado. A SSH fica aberta porque o IP da internet de casa muda, mas só entra
quem tiver a chave (o passo 4.2 desliga o login por senha).

### 3.4 DNS da API
Cloudflare → domínio → **DNS** → **Add record**:

| Tipo | Nome | Valor | Proxy |
|---|---|---|---|
| A | `api` | o IP da VPS | **nuvem cinza (DNS only)** |

> A nuvem **tem** que ficar cinza. Se ficar laranja, a Cloudflare passa a ficar entre o visitante e o Caddy: o Caddy
> não consegue tirar o certificado e a API passa a enxergar o IP da Cloudflare em vez do IP do visitante (o rate
> limiting quebra).

✅ **Confere** (no PowerShell do PC): `nslookup api.seudominio.com.br` responde o IP da VPS.

---

## 4. Entrar na máquina e preparar

### 4.1 Primeiro acesso (como root)
```powershell
ssh -i $HOME\.ssh\chale-vps root@api.seudominio.com.br
```
Na primeira vez ele pergunta "Are you sure...?". Digite `yes`.

Crie um usuário seu (o dia a dia não roda como root) e copie a chave para ele:
```bash
adduser chale                     # escolha uma senha (é a do sudo) e aperte Enter no resto
usermod -aG sudo chale
mkdir -p /home/chale/.ssh
cp ~/.ssh/authorized_keys /home/chale/.ssh/
chown -R chale:chale /home/chale/.ssh
chmod 700 /home/chale/.ssh && chmod 600 /home/chale/.ssh/authorized_keys
```

### 4.2 Desligar login por senha e login do root
```bash
printf 'PasswordAuthentication no\nPermitRootLogin no\n' > /etc/ssh/sshd_config.d/01-chale.conf
systemctl restart ssh
```
> O nome começa com `01-` de propósito: o SSH usa o **primeiro** valor que encontra, e assim este arquivo ganha de
> qualquer outro que a Hostinger tenha criado.

**Não feche esta janela ainda.** Abra **outro** PowerShell e teste:
```powershell
ssh -i $HOME\.ssh\chale-vps chale@api.seudominio.com.br
```
Entrou? Pode fechar a janela do root. Não entrou? Volte na janela do root e confira o 4.1. Se você se trancar
fora, o hPanel tem um **terminal no navegador** que entra mesmo assim.

Daqui até o passo 7, tudo roda **no servidor**, com o usuário `chale`.

### 4.3 Atualizar e instalar o Docker
```bash
sudo apt update && sudo apt upgrade -y
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker chale
exit
```
Entre de novo (é preciso sair e entrar para o grupo `docker` valer).

✅ **Confere:** `docker run --rm hello-world` mostra "Hello from Docker!".

### 4.4 Baixar o código da API
O repositório é privado, então o servidor precisa de uma chave **só de leitura** (deploy key):

```bash
ssh-keygen -t ed25519 -C "chale-vps-deploy" -f ~/.ssh/id_ed25519 -N ""
cat ~/.ssh/id_ed25519.pub
```
Copie a linha que aparecer. No GitHub: repositório **ChaleAltoDoMorroAPI** → **Settings** → **Deploy keys** →
**Add deploy key** → cole → **NÃO** marque "Allow write access" → **Add key**.

```bash
git clone -b master git@github.com:PietroTressoldi/ChaleAltoDoMorroAPI.git ~/chale
cd ~/chale
```

✅ **Confere:** `ls` mostra `Dockerfile`, `ChaleAltoDoMorroAPI`, `docs`...

---

## 5. Os arquivos de produção

Ficam na pasta `~/chale` do servidor, ao lado do `Dockerfile`. Dois deles **já vêm do git**:

| Arquivo | O que é | Precisa mexer? |
|---|---|---|
| `docker-compose.prod.yml` | Sobe os 3 containers: Postgres, API e Caddy. O banco e a API **não** abrem porta; só o Caddy abre (80/443). | Não |
| `Caddyfile` | Diz ao Caddy: "o domínio `API_DOMAIN` vai para a API na porta 8080". | Não (o domínio vem do `.env`) |
| `.env` | Senhas e configurações. **Nunca** vai para o git: é criado só no servidor. | **Sim**, no passo abaixo |

### 5.1 `.env` (só no servidor)
Gere os segredos **no servidor** e anote no gerenciador de senhas:
```bash
openssl rand -hex 32       # → senha do banco (POSTGRES_PASSWORD e Password= da connection string)
openssl rand -base64 48    # → Jwt__Key
```
Crie o arquivo a partir do modelo e abra para editar:
```bash
cp docs/deploy/.env.example .env
nano .env
```
Troque todos os `<...>` e o `seudominio.com.br` das **Partes 1 e 2**. As Partes 3 e 4 (dos fronts) estão comentadas
com `#` e podem ficar ali, não atrapalham. Para salvar no nano: `Ctrl+O`, `Enter`, `Ctrl+X`.

```bash
chmod 600 .env
```

### 5.2 Atalho para não digitar `-f docker-compose.prod.yml` toda hora
```bash
echo "alias dc='docker compose -f ~/chale/docker-compose.prod.yml --project-directory ~/chale'" >> ~/.bashrc
source ~/.bashrc
```
Daqui em diante, `dc` é o mesmo que `docker compose -f docker-compose.prod.yml`.

✅ **Confere:** `dc config` mostra o compose com os valores do `.env` preenchidos, sem erro.

---

## 6. Subir o banco e criar as tabelas

A API **não** cria as tabelas sozinha (decisão da Task 12) e **precisa** delas para subir, porque o admin inicial
é criado na subida. Então a ordem é: **banco → tabelas → API**.

### 6.1 Subir só o banco
```bash
dc up -d postgres
```
✅ **Confere:** `dc ps` mostra o postgres como `healthy`.

### 6.2 Gerar o script das tabelas (no **PC**)
No PowerShell do PC, na pasta da API, **na branch `master`**:
```powershell
dotnet ef migrations script --idempotent --project ChaleAltoDoMorroAPI -o migrate.sql
scp -i $HOME\.ssh\chale-vps migrate.sql chale@api.seudominio.com.br:~/chale/
```
- `--idempotent` quer dizer que o script confere quais migrations já rodaram e só aplica as que faltam. Pode rodar
  quantas vezes quiser, em banco vazio ou cheio.
- Não faça commit do `migrate.sql` (apague depois).

### 6.3 Aplicar o script (no **servidor**)
```bash
cd ~/chale
dc exec -T postgres psql -v ON_ERROR_STOP=1 -U chale_admin -d chale_alto_do_morro < migrate.sql
rm migrate.sql
```
✅ **Confere:** termina com `COMMIT` e sem `ERROR`. Para ver as tabelas:
`dc exec postgres psql -U chale_admin -d chale_alto_do_morro -c '\dt'`

---

## 7. Subir a API e o Caddy

```bash
dc up -d --build
```
Na primeira vez demora alguns minutos (ele baixa o .NET e compila).

✅ **Confere:**
```bash
dc ps                       # os 3 com status "Up" / "running"
dc logs api --tail 50       # procure "Admin inicial criado." e nenhuma exceção
dc logs caddy --tail 50     # procure "certificate obtained successfully"
curl https://api.seudominio.com.br/health
```
O `curl` tem que responder `{"status":"ok"}`. Abra a mesma URL no navegador: tem que aparecer o **cadeado**.

---

## 8. Os sites na Cloudflare

### 8.0 Landing: já está no ar (Workers)
O projeto `chalealtodomorro` já publica o `main` em `chalealtodomorro.pietrotressoldidev.workers.dev`. O passo a passo
fica em `docs/PUBLICACAO.md`, no repositório da landing. Para ligar na API:
1. Merge `develop` → `main` na landing.
2. Workers → `chalealtodomorro` → **Settings → Variables** (de build): `VITE_API_URL=https://api.seudominio.com.br`
   e `VITE_SITE_URL=https://seudominio.com.br`.
3. **Settings → Domains & Routes → Add → Custom domain**: `seudominio.com.br` e `www.seudominio.com.br`.
4. Gere um build novo (push no `main` ou **Deployments → Retry**).

### Portal: Cloudflare Pages
Faça o 8.1 e o 8.2 só para o **portal**. A coluna da landing fica de referência, caso um dia ela migre para o Pages.

| | Landing | Portal |
|---|---|---|
| Repositório | `chale-alto-do-morro-landing` | `ChaleAltoDoMorroGestao` |
| Nome do projeto no Pages | `chale-landing` | `chale-portal` |
| Variáveis de ambiente | `VITE_API_URL=https://api.seudominio.com.br`<br>`VITE_META_PIXEL_ID=<id ou vazio>` | `VITE_API_URL=https://api.seudominio.com.br`<br>`VITE_USE_MOCKS=false` |
| Domínio | `seudominio.com.br` e `www.seudominio.com.br` | `portal.seudominio.com.br` |

### 8.1 Criar o projeto
Cloudflare → **Workers & Pages** → **Create** → aba **Pages** → **Connect to Git** → autorize o GitHub e escolha
**só** o repositório da tabela.

| Campo | O que colocar |
|---|---|
| Production branch | landing: `main`. Portal: só existe `develop`; crie a `master` (`git checkout -b master` → `git push -u origin master`) |
| Framework preset | **React (Vite)** |
| Build command | `npm run build` |
| Build output directory | `dist` |
| Environment variables | as da tabela (Parte 3 ou 4 do [`.env.example`](.env.example)), mais `NODE_VERSION=22` |

**Save and Deploy**. Ele faz o primeiro build e publica num endereço `chale-landing.pages.dev`.

> As variáveis `VITE_` entram **dentro** do site no momento do build. Mudou uma delas no painel? Vá em
> **Deployments** → **Retry deployment** para gerar o site de novo. Nada disso é segredo: qualquer pessoa vê.

✅ **Confere:** o deploy fica verde e `https://chale-landing.pages.dev` abre. Os dados da API ainda **não** carregam
por esse endereço, porque o CORS só libera o domínio próprio. Isso fica certo no 8.2.

### 8.2 Domínio próprio
Projeto → **Custom domains** → **Set up a custom domain** → digite o domínio da tabela → **Activate domain**.
Como o DNS já está na Cloudflare, ela cria o registro sozinha. Na landing, repita para o `www`.

✅ **Confere:** `https://seudominio.com.br`, `https://www.seudominio.com.br` e `https://portal.seudominio.com.br`
abrem com cadeado. No portal, abra `/reservas` (ou outra tela) e aperte **F5**: a página tem que carregar.

> A landing já tem um `public/_headers`. Esse é o formato do Cloudflare Pages, então ele passa a valer sozinho.

---

## 9. Teste final (faça tudo)

> O banco de produção nasce **vazio**: o seed só roda em Development. Antes de testar (e antes de divulgar o site),
> cadastre pelo portal os **preços**, o **mínimo de diárias**, os **kits**, os **pacotes**, as **fotos** e os **vídeos**.

- [ ] `https://api.seudominio.com.br/health` → `{"status":"ok"}`.
- [ ] A landing mostra os preços **do banco**: mude um preço no portal, recarregue o site e veja o preço mudar.
- [ ] O login no portal funciona. **F12 → Console** sem erro vermelho de **CORS**.
- [ ] Envie uma foto pela galeria do portal, veja ela aparecer no site e apague.
- [ ] Faça uma pré-reserva de teste no site, veja ela aparecer no portal e apague ou cancele.
- [ ] Troque a senha do admin no portal. Depois, no servidor: apague as linhas `AdminSeed__*` do `.env` e rode
      `dc up -d` (recria a API sem elas).
- [ ] Todos os 🔒 estão no gerenciador de senhas: senha do banco, `Jwt__Key`, chave `chale-vps`, senha do root e
      senha do usuário `chale`.

---

## 10. Backup (não pule)

### 10.1 Cópia do banco, todo dia
No servidor:
```bash
mkdir -p ~/backups
crontab -e        # se ele perguntar o editor, escolha 1 (nano)
```
Cole no final (é uma linha só) e salve:
```
0 3 * * * cd ~/chale && docker compose -f docker-compose.prod.yml exec -T postgres pg_dump -U chale_admin -Fc chale_alto_do_morro > ~/backups/chale_$(date +\%F).dump && find ~/backups -name '*.dump' -mtime +14 -delete
```
Todo dia às 03:00 ele gera um `.dump` e apaga os que têm mais de 14 dias. A VPS da Hostinger vem no horário de
Brasília; confira com `date`. Se ela estiver em UTC, troque o `0 3` por `0 6`.

✅ **Confere** no dia seguinte: `ls -lh ~/backups` mostra um arquivo com tamanho maior que zero.

### 10.2 Cópia fora da VPS
O `.dump` do 10.1 fica **dentro** da VPS: se a máquina morrer, ele morre junto.
- A Hostinger faz um **backup semanal** da VPS inteira (banco, fotos e tudo). Confira em hPanel → VPS →
  **Backups e monitoramento** se ele está ligado.
- **Uma vez por mês**, baixe os `.dump` para o PC (as fotos ficam cobertas pelo backup semanal):
  ```powershell
  scp -i $HOME\.ssh\chale-vps "chale@api.seudominio.com.br:~/backups/*.dump" D:\Backups\Chale\
  ```
  Teste restaurar no Docker local de vez em quando. Backup que nunca foi restaurado não é backup.

> Quando isso virar trabalho demais: dá para mandar o `.dump` para o **Cloudflare R2** (10 GB grátis) com o `rclone`,
> no mesmo cron.

---

## 11. Dia a dia

### Publicar uma versão nova da API
1. No PC: testes verdes no `develop` → merge `develop` → `master` → push.
2. **Teve migration nova?** Faça o passo **6.2 e 6.3** de novo (o script é idempotente, pode rodar sempre).
3. No servidor:
   ```bash
   cd ~/chale
   git pull
   dc up -d --build
   curl https://api.seudominio.com.br/health
   ```
   > Migration que **apaga ou renomeia** coluna: aplique o script **junto** com o `dc up -d --build`, e não horas antes.
   > Senão a API antiga quebra no meio do caminho.

### Publicar uma versão nova da landing ou do portal
É só dar `git push` na branch de produção do repositório. A Cloudflare faz o build e publica sozinha (acompanhe em
**Deployments**). Se a versão nova quebrar, abra **Deployments**, escolha a anterior e clique em **Rollback**.

### Comandos úteis no servidor
| Quero... | Comando |
|---|---|
| Ver o que está rodando | `dc ps` |
| Ver o log da API ao vivo | `dc logs -f api` (para sair: `Ctrl+C`) |
| Reiniciar a API | `dc restart api` |
| Aplicar uma mudança no `.env` | `dc up -d` (recria só quem mudou) |
| Entrar no banco | `dc exec postgres psql -U chale_admin -d chale_alto_do_morro` (para sair: `\q`) |
| Ver o espaço em disco | `df -h` |
| Limpar imagens velhas do Docker | `docker image prune -f` |
| Atualizar o Linux | `sudo apt update && sudo apt upgrade -y` (depois `sudo reboot` se ele pedir) |

---

## 12. Deu ruim? (problemas comuns)

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| `ssh` dá `Permission denied (publickey)` | Faltou o `-i $HOME\.ssh\chale-vps`, ou o usuário está errado | Entre com o usuário `chale` e com a chave. Se não der: use o terminal do hPanel |
| `ssh` trava ou dá timeout | A porta 22 está fechada no firewall | Passo 3.3 |
| API reiniciando sem parar (`dc ps` mostra "Restarting") | Variável errada | `dc logs api --tail 80`. Mensagens comuns: **Jwt:Key ausente ou curta**, **Cors:...** (barra no fim ou `http`), **Storage:PublicBaseUrl** |
| Log da API: `relation "..." does not exist` | As tabelas não foram criadas | Passo 6.2 e 6.3 |
| Log da API: `password authentication failed` | A senha do `.env` é diferente da do banco | Confira se `POSTGRES_PASSWORD` é igual ao `Password=` da connection string. Se trocou a senha depois de criar o banco, o Postgres ignorou (veja o aviso na Parte 1 do `.env.example`) |
| O Caddy não tira o certificado | DNS do `api` errado, porta 80/443 fechada ou nuvem **laranja** | `nslookup api.seudominio.com.br` tem que dar o IP da VPS. Confira o passo 3.3 e o 3.4 |
| O site abre mas sem preços, ou com dados falsos | `VITE_API_URL` vazia ou errada no Pages | Settings → Environment variables → corrija → **Retry deployment** |
| Console do navegador com erro de **CORS** | A origem no `.env` da API é diferente da URL do site | `Cors__...` tem que ser **exatamente** a URL do site: `https`, sem barra no fim e com ou sem `www`, igual ao que você abriu. Testando pelo `*.pages.dev` vai dar CORS: teste pelo domínio próprio |
| O portal mostra dados falsos | `VITE_USE_MOCKS` ficou `true` ou não foi definida | Settings → Environment variables → `VITE_USE_MOCKS=false` → **Retry deployment** |
| Muitos erros **429** para todo mundo, ninguém consegue logar | A API está vendo o IP do Caddy (ou da Cloudflare) em vez do visitante | A nuvem do `api` tem que ser **cinza**. Se for, a rede do Docker saiu das faixas confiáveis (`172.16.0.0/12` e `192.168.0.0/16`, em `Global/ForwardedHeadersSetup.cs`). Veja a faixa com `docker network inspect chale_default` |
| A foto enviada não aparece no site | `Storage__PublicBaseUrl` errado | Tem que ser `https://api.seudominio.com.br/uploads/` (com barra no fim) |
| Disco cheio | Imagens velhas do Docker ou backups | `docker image prune -f` e `ls -lh ~/backups` |
