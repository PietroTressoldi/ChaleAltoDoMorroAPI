# Deploy na AWS — passo a passo

> **Plano B.** O deploy escolhido é o [`DEPLOY_HOSTINGER.md`](DEPLOY_HOSTINGER.md). Este guia fica de referência.

API + banco numa **EC2**. Landing e portal no **S3**, com o **CloudFront** na frente.
Siga **na ordem**. Cada passo termina com um **✅ Confere**: só avance se ele deu certo.

> Troque `seudominio.com.br` pelo domínio real em **todos** os lugares.
> Todas as variáveis estão explicadas em [`docs/deploy/.env.example`](.env.example).

---

## 0. Como tudo vai ficar

```
                       ┌──────────── EC2 (uma máquina Linux) ────────────┐
 api.seudominio  ───►  │  Caddy (HTTPS)  ──►  API .NET  ──►  Postgres    │
                       │  portas 80/443        :8080          (fechado)  │
                       │                       fotos no disco (volume)   │
                       └─────────────────────────────────────────────────┘

 seudominio      ───►  CloudFront ──► bucket S3 "landing"   (arquivos do build do site)
 portal.seudominio ─►  CloudFront ──► bucket S3 "portal"    (arquivos do build do portal)
```

- **Caddy** é um servidor web que pega o certificado HTTPS sozinho (Let's Encrypt) e repassa para a API.
- **CloudFront** dá HTTPS para os sites e faz o React funcionar quando alguém abre `/reservas` direto.
- O **Postgres não fica aberto** para a internet. Só a API fala com ele, por dentro do Docker.

Custo aproximado: EC2 `t3.small` + 20 GB de disco ≈ US$ 25–30/mês em São Paulo. S3 + CloudFront de um site pequeno
fica em centavos. Confira na calculadora da AWS antes.

---

## 1. Antes de começar (checklist)

- [ ] Conta na AWS com **cartão cadastrado** e **MFA** (autenticação em 2 fatores) no usuário root.
- [ ] Domínio comprado e acesso ao painel de **DNS** dele (Registro.br, Cloudflare, Route 53...).
- [ ] Gerenciador de senhas aberto (Bitwarden, 1Password...) com um item **"Chalé — produção"**.
- [ ] No PC: `git`, `dotnet`, `node` e o **AWS CLI** ([instalador para Windows](https://aws.amazon.com/cli/)).
- [ ] Tudo testado e commitado no `develop`, e o merge `develop` → `master` feito (o servidor baixa a `master`).

> **Região:** use **São Paulo (`sa-east-1`)** para EC2 e S3. A **única** exceção é o certificado do CloudFront
> (passo 2), que **tem** que ser em **N. Virginia (`us-east-1`)**. É regra da AWS.

---

## 2. Pedir o certificado HTTPS dos sites (faça primeiro, ele demora)

1. Console AWS → troque a região (canto superior direito) para **US East (N. Virginia) us-east-1**.
2. Procure **Certificate Manager (ACM)** → **Request** → **Request a public certificate**.
3. Domain names (clique em "Add another name" para cada um):
   - `seudominio.com.br`
   - `www.seudominio.com.br`
   - `portal.seudominio.com.br`
4. Validation method: **DNS validation** → **Request**.
5. Abra o certificado. Para cada domínio aparece um **CNAME** (Name + Value). Crie esses CNAMEs no painel de DNS do
   seu domínio. (Se o DNS estiver no Route 53, tem um botão **"Create records in Route 53"** que faz sozinho.)
6. Volte para a região **São Paulo** e siga para o passo 3 enquanto ele valida.

✅ **Confere** (pode levar de 5 min a algumas horas): o status do certificado fica **Issued**.

---

## 3. Criar a máquina (EC2)

Console → região **São Paulo** → **EC2** → **Launch instance**.

| Campo | O que colocar |
|---|---|
| Name | `chale-api` |
| AMI | **Ubuntu Server 24.04 LTS** (x86) |
| Instance type | **t3.small** (2 GB de RAM) |
| Key pair | **Create new key pair** → nome `chale-ec2`, tipo RSA, formato `.pem` → **baixa um arquivo**. Guarde (sem ele não entra na máquina) e coloque uma cópia no gerenciador de senhas. |
| Network → Security group | **Create security group** com 3 regras: |
| | SSH (22) → Source: **My IP** (só o seu IP) |
| | HTTP (80) → Source: **Anywhere** (`0.0.0.0/0`) |
| | HTTPS (443) → Source: **Anywhere** (`0.0.0.0/0`) |
| Storage | **20 GiB**, tipo **gp3** |

Clique em **Launch instance**.

**IP fixo (Elastic IP)** — sem isso, o IP muda se a máquina reiniciar:
1. EC2 → **Elastic IPs** → **Allocate Elastic IP address** → **Allocate**.
2. Selecione o IP → **Actions** → **Associate Elastic IP address** → escolha a instância `chale-api` → **Associate**.
3. Anote o IP (ex.: `18.230.10.20`).

**DNS da API:** no painel de DNS do domínio, crie:

| Tipo | Nome | Valor |
|---|---|---|
| A | `api` | o Elastic IP |

> Se o DNS estiver na Cloudflare: deixe a nuvem **cinza** ("DNS only") nesse registro. Com a nuvem laranja o Caddy não
> consegue pegar o certificado.

✅ **Confere** (no PowerShell do PC): `nslookup api.seudominio.com.br` responde o seu Elastic IP.

---

## 4. Entrar na máquina e preparar

No PowerShell do PC, na pasta onde está o `chale-ec2.pem`:

```powershell
ssh -i chale-ec2.pem ubuntu@api.seudominio.com.br
```

Na primeira vez ele pergunta "Are you sure...?" → digite `yes`.
(Se der erro de permissão do `.pem` no Windows: botão direito no arquivo → Propriedades → Segurança → deixe só o seu
usuário com acesso.)

Agora você está **dentro do servidor**. Tudo daqui até o passo 7 roda **lá**.

### 4.1 Memória extra (swap) — o build do .NET precisa
```bash
sudo fallocate -l 2G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

### 4.2 Atualizar e instalar o Docker
```bash
sudo apt update && sudo apt upgrade -y
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
exit
```
Entre de novo com o mesmo `ssh -i ...` (precisa sair e entrar para valer o grupo `docker`).

✅ **Confere:** `docker run --rm hello-world` mostra "Hello from Docker!".

### 4.3 Baixar o código da API
O repositório é privado, então o servidor precisa de uma chave **só de leitura** (deploy key):

```bash
ssh-keygen -t ed25519 -C "chale-ec2" -f ~/.ssh/id_ed25519 -N ""
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
| `docker-compose.prod.yml` | Liga os 3 containers: Postgres, API e Caddy. O banco e a API **não** abrem porta; só o Caddy (80/443). | Não |
| `Caddyfile` | Diz ao Caddy: "o domínio `API_DOMAIN` vai para a API na porta 8080". | Não (o domínio vem do `.env`) |
| `.env` | Senhas e configurações. **Nunca** vai para o git: é criado só no servidor. | **Sim**, passo abaixo |

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
com `#` e podem ficar ali, não atrapalham. Salvar no nano: `Ctrl+O`, `Enter`, `Ctrl+X`.

```bash
chmod 600 .env
```

### 5.2 Atalho para não digitar `-f docker-compose.prod.yml` toda hora
```bash
echo "alias dc='docker compose -f ~/chale/docker-compose.prod.yml --project-directory ~/chale'" >> ~/.bashrc
source ~/.bashrc
```
Daqui em diante, `dc` = `docker compose -f docker-compose.prod.yml`.

✅ **Confere:** `dc config` mostra o compose com os valores do `.env` preenchidos, sem erro.

---

## 6. Subir o banco e criar as tabelas

A API **não** cria as tabelas sozinha (decisão da Task 12). E ela **precisa** das tabelas para subir (o admin inicial
é criado na subida). Então a ordem é: **banco → tabelas → API**.

### 6.1 Subir só o banco
```bash
dc up -d postgres
```
✅ **Confere:** `dc ps` mostra o postgres como `healthy`.

### 6.2 Gerar o script das tabelas (no **PC**)
No PowerShell do PC, na pasta da API, **na branch `master`**:
```powershell
dotnet ef migrations script --idempotent --project ChaleAltoDoMorroAPI -o migrate.sql
scp -i caminho\para\chale-ec2.pem migrate.sql ubuntu@api.seudominio.com.br:~/chale/
```
- `--idempotent` = o script confere quais migrations já rodaram e só aplica as que faltam. Pode rodar quantas vezes
  quiser, em banco vazio ou cheio.
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
A primeira vez demora uns minutos (baixa o .NET e compila).

✅ **Confere:**
```bash
dc ps                       # os 3 com status "Up" / "running"
dc logs api --tail 50       # procure "Admin inicial criado." e nenhuma exceção
dc logs caddy --tail 50     # procure "certificate obtained successfully"
curl https://api.seudominio.com.br/health
```
O `curl` tem que responder `{"status":"ok"}`. Abra a mesma URL no navegador: tem que aparecer o **cadeado**.

---

## 8. Os sites no S3 + CloudFront

Faça o 8.1 a 8.3 **duas vezes**: uma para a **landing** e outra para o **portal**.

| | Landing | Portal |
|---|---|---|
| Nome do bucket | `chale-landing-site` | `chale-portal-site` |
| Domínios no CloudFront | `seudominio.com.br` e `www.seudominio.com.br` | `portal.seudominio.com.br` |

> Nome de bucket é único no **mundo todo**. Se já existir, coloque um sufixo (ex.: `chale-landing-site-2026`).

### 8.1 Criar o bucket
Console → região **São Paulo** → **S3** → **Create bucket**:
- Bucket name: o da tabela.
- **Block all public access: deixe MARCADO.** O bucket fica privado; só o CloudFront lê.
- O resto padrão → **Create bucket**.

### 8.2 Criar o CloudFront
Console → **CloudFront** → **Create distribution**:
| Campo | O que colocar |
|---|---|
| Origin domain | escolha o bucket na lista (`chale-landing-site.s3...`) |
| Origin access | **Origin access control settings (recommended)** → **Create new OAC** → Create |
| Viewer protocol policy | **Redirect HTTP to HTTPS** |
| Cache policy | **CachingOptimized** |
| Web Application Firewall (WAF) | **Do not enable security protections** (pode ligar depois; é pago) |
| Alternate domain name (CNAME) | os domínios da tabela |
| Custom SSL certificate | o certificado do passo 2 |
| Default root object | `index.html` |

**Create distribution**. Logo depois aparece um aviso amarelo com a **política do bucket**:
clique em **Copy policy** → abra o bucket → **Permissions** → **Bucket policy** → **Edit** → cole → **Save**.
(Se o console oferecer o botão para atualizar a política sozinho, use ele.)

### 8.3 Fazer o React funcionar em qualquer rota
Na distribuição → aba **Error pages** → **Create custom error response**, duas vezes:

| HTTP error code | Customize error response | Response page path | HTTP response code |
|---|---|---|---|
| 403 | Yes | `/index.html` | 200 |
| 404 | Yes | `/index.html` | 200 |

**Por quê:** `/reservas` não é um arquivo no S3, é uma rota do React. Sem isso, recarregar a página dá erro.

Anote o **Distribution ID** (ex.: `E1ABCDEF2GHIJ`) e o **Distribution domain name** (ex.: `d111abc.cloudfront.net`)
de cada uma.

### 8.4 DNS dos sites
No painel de DNS do domínio:

| Nome | Tipo | Aponta para |
|---|---|---|
| `portal` | CNAME | `dxxxx.cloudfront.net` do **portal** |
| `www` | CNAME | `dxxxx.cloudfront.net` da **landing** |
| `@` (o domínio puro) | veja abaixo | `dxxxx.cloudfront.net` da **landing** |

O domínio puro (`seudominio.com.br`, sem nada na frente) **não aceita CNAME normal**:
- **Route 53:** registro tipo **A** → liga **Alias** → "Alias to CloudFront distribution" → escolha a da landing.
- **Cloudflare:** pode criar CNAME em `@` (ela faz "flattening"). Nuvem **cinza** (DNS only).
- **Registro.br** (DNS deles): não tem como. Mude o DNS do domínio para o Route 53 (≈ US$ 0,50/mês) ou para a
  Cloudflare (grátis).

✅ **Confere:** `nslookup portal.seudominio.com.br` responde endereços da AWS (pode levar alguns minutos).

### 8.5 Usuário para enviar os sites pelo PC
Console → **IAM** → **Users** → **Create user** → nome `chale-deploy-sites` (sem acesso ao console) →
**Attach policies directly** → **Create policy** → aba **JSON** → cole (troque os nomes dos buckets se mudou):

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": ["arn:aws:s3:::chale-landing-site", "arn:aws:s3:::chale-portal-site"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:PutObject", "s3:DeleteObject"],
      "Resource": ["arn:aws:s3:::chale-landing-site/*", "arn:aws:s3:::chale-portal-site/*"]
    },
    {
      "Effect": "Allow",
      "Action": ["cloudfront:CreateInvalidation"],
      "Resource": "*"
    }
  ]
}
```
Nome da policy: `chale-deploy-sites` → crie → volte e marque ela no usuário → **Create user**.

Abra o usuário → **Security credentials** → **Create access key** → "Command Line Interface (CLI)" → copie as duas
chaves (**guarde no gerenciador de senhas**, a secreta só aparece uma vez).

No PowerShell do PC:
```powershell
aws configure
# AWS Access Key ID:     <a chave>
# AWS Secret Access Key: <a chave secreta>
# Default region name:   sa-east-1
# Default output format: json
```
Essa chave só consegue mexer nesses 2 buckets. Mesmo se vazar, não dá acesso ao resto da conta.

### 8.6 Build e envio da LANDING (no PC)
Na pasta `D:\Desenvolvimento\ChaleAltoDoMorroLandingPage`, crie o arquivo `.env.production` (Parte 3 do
[`.env.example`](.env.example)):
```
VITE_API_URL=https://api.seudominio.com.br
VITE_META_PIXEL_ID=<id-do-pixel ou deixe vazio>
```
Depois:
```powershell
npm ci
npm run build
aws s3 sync dist s3://chale-landing-site --delete
aws cloudfront create-invalidation --distribution-id <ID-DA-LANDING> --paths "/*"
```
- `sync --delete` = envia o que mudou e apaga do bucket o que não existe mais no `dist`.
- `create-invalidation` = manda o CloudFront esquecer a versão antiga. Sem isso, o site novo demora até 24 h para
  aparecer.

### 8.7 Build e envio do PORTAL (no PC)
Na pasta `D:\Desenvolvimento\ChaleAltoDoMorroGestao`, crie o `.env.production` (Parte 4):
```
VITE_API_URL=https://api.seudominio.com.br
VITE_USE_MOCKS=false
```
```powershell
npm ci
npm run build
aws s3 sync dist s3://chale-portal-site --delete
aws cloudfront create-invalidation --distribution-id <ID-DO-PORTAL> --paths "/*"
```

> O `.env.production` não tem segredo (tudo nele fica público no site), então tanto faz se for para o git.
> No portal o `.gitignore` já ignora; na landing, não.

✅ **Confere:** `https://seudominio.com.br` e `https://portal.seudominio.com.br` abrem com cadeado.

---

## 9. Teste final (faça tudo)

- [ ] `https://api.seudominio.com.br/health` → `{"status":"ok"}`.
- [ ] Landing mostra os preços **do banco**: mude um preço no portal, recarregue o site, o preço muda.
- [ ] Login no portal funciona. **F12 → Console** sem erro vermelho de **CORS**.
- [ ] No portal, abra `/reservas` (ou outra tela) e aperte **F5**: a página carrega (não dá erro 403/404).
- [ ] Envie uma foto pela galeria do portal → aparece no site → apague.
- [ ] Faça uma pré-reserva de teste no site → aparece no portal → apague/cancele.
- [ ] Troque a senha do admin no portal. Depois, no servidor: apague as linhas `AdminSeed__*` do `.env` e rode
      `dc up -d` (recria a API sem elas).
- [ ] Todos os 🔒 estão no gerenciador de senhas (senha do banco, Jwt__Key, `.pem`, chaves do `chale-deploy-sites`).

---

## 10. Backup (não pule)

### 10.1 Foto do disco inteiro, todo dia (pega banco + fotos + tudo)
Console → **EC2** → **Lifecycle Manager** → **Create lifecycle policy** → **EBS snapshot policy**:
- Target: **Instance**, tag `Name` = `chale-api`.
- Schedule: **Daily**, horário de madrugada (está em UTC: 06:00 UTC = 03:00 de Brasília).
- Retention: **7** (guarda os últimos 7 dias).
- Create.

### 10.2 Cópia só do banco, todo dia (mais fácil de restaurar)
No servidor:
```bash
mkdir -p ~/backups
crontab -e        # se perguntar o editor, escolha 1 (nano)
```
Cole no final (uma linha só) e salve:
```
0 6 * * * cd ~/chale && docker compose -f docker-compose.prod.yml exec -T postgres pg_dump -U chale_admin -Fc chale_alto_do_morro > ~/backups/chale_$(date +\%F).dump && find ~/backups -name '*.dump' -mtime +14 -delete
```
Todo dia às 03:00 de Brasília gera um `.dump` e apaga os com mais de 14 dias.

✅ **Confere** no dia seguinte: `ls -lh ~/backups` mostra um arquivo com tamanho maior que zero.

> De vez em quando, baixe um `.dump` para o PC (`scp`) e teste restaurar no Docker local. Backup que nunca foi
> restaurado não é backup.

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
   > Migration que **apaga ou renomeia** coluna: aplique o script **junto** com o `dc up -d --build`, não horas antes,
   > senão a API antiga quebra no meio do caminho.

### Publicar uma versão nova da landing ou do portal
No PC, na pasta do projeto: os comandos do **8.6** (landing) ou **8.7** (portal). São só os 4: `npm ci`, `npm run build`,
`aws s3 sync`, `aws cloudfront create-invalidation`.

### Comandos úteis no servidor
| Quero... | Comando |
|---|---|
| Ver o que está rodando | `dc ps` |
| Ver o log da API ao vivo | `dc logs -f api` (sair: `Ctrl+C`) |
| Reiniciar a API | `dc restart api` |
| Mudei o `.env` | `dc up -d` (recria só quem mudou) |
| Entrar no banco | `dc exec postgres psql -U chale_admin -d chale_alto_do_morro` (sair: `\q`) |
| Ver espaço em disco | `df -h` |
| Limpar imagens velhas do Docker | `docker image prune -f` |
| Atualizar o Linux | `sudo apt update && sudo apt upgrade -y` (depois `sudo reboot` se pedir) |

---

## 12. Deu ruim? (problemas comuns)

| Sintoma | Causa provável | O que fazer |
|---|---|---|
| `ssh` trava ou dá timeout | Seu IP mudou (internet de casa troca IP) | EC2 → Security groups → regra SSH → Source **My IP** de novo |
| API reiniciando sem parar (`dc ps` mostra "Restarting") | Variável errada | `dc logs api --tail 80`. Mensagens comuns: **Jwt:Key ausente ou curta**, **Cors:...** (barra no fim ou `http`), **Storage:PublicBaseUrl** |
| Log da API: `relation "..." does not exist` | Tabelas não criadas | Passo 6.2 e 6.3 |
| Log da API: `password authentication failed` | Senha do `.env` diferente da do banco | Confira se `POSTGRES_PASSWORD` = `Password=` da connection string. Se trocou a senha depois de criar o banco, o Postgres ignorou (ver aviso na Parte 1 do `.env.example`) |
| Caddy não pega certificado | DNS do `api` errado, ou porta 80/443 fechada, ou nuvem laranja da Cloudflare | `nslookup api.seudominio.com.br` → tem que dar o Elastic IP. Confira o security group |
| Site abre mas sem preços / dados falsos | `VITE_API_URL` vazia ou errada no build | Confira o `.env.production` → build → sync → invalidation |
| Console do navegador: erro de **CORS** | Origem no `.env` da API diferente da URL do site | `Cors__...` tem que ser **exatamente** a URL do site: `https`, sem barra no fim, com ou sem `www` igual ao que você abriu |
| Site mostra versão antiga | Cache do CloudFront | `aws cloudfront create-invalidation ... --paths "/*"` |
| Portal: F5 em `/reservas` dá **AccessDenied** | Faltaram as páginas de erro | Passo 8.3 |
| Site/portal: **AccessDenied** até na página inicial | Política do bucket não colada, ou "Default root object" vazio | Passo 8.2 |
| Muitos erros **429** para todo mundo / ninguém consegue logar | A API está vendo o IP do Caddy em vez do visitante | A rede do Docker saiu das faixas confiáveis (`172.16.0.0/12` e `192.168.0.0/16`, em `Global/ForwardedHeadersSetup.cs`). Veja a faixa com `docker network inspect chale_default` |
| Foto enviada não aparece no site | `Storage__PublicBaseUrl` errado | Tem que ser `https://api.seudominio.com.br/uploads/` (com barra no fim) |

---

## Extra (opcional): fotos no S3 em vez do disco

Hoje as fotos ficam no disco da EC2 (volume `uploads`), e o backup do passo 10.1 já cobre. Só mude se o disco começar a
encher ou se quiser que as fotos carreguem mais rápido pelo CloudFront.

1. Crie um bucket `chale-uploads` (privado) + um CloudFront para ele, igual ao 8.1 e 8.2, **sem** as páginas de erro
   do 8.3 e sem domínio próprio (pode usar o `dxxxx.cloudfront.net`).
2. IAM → **Roles** → **Create role** → AWS service → **EC2** → crie uma policy permitindo `s3:PutObject` e
   `s3:DeleteObject` **só** em `arn:aws:s3:::chale-uploads/*` → nome da role `chale-api-ec2`.
3. EC2 → instância → **Actions** → **Security** → **Modify IAM role** → `chale-api-ec2`.
4. No `.env` do servidor, troque o bloco de Storage pelo comentado no `.env.example` (`Storage__Provider=S3`...).
5. Copie as fotos que já existem:
   `docker run --rm -v chale_uploads:/u amazon/aws-cli s3 sync /u s3://chale-uploads/`
   (o nome do volume aparece em `docker volume ls`; a AWS CLI usa a role da máquina).
6. `dc up -d` → envie uma foto pelo portal, abra a URL, apague. (O `S3FileStorage` nunca foi testado num bucket real:
   esse teste é obrigatório.)

> No Docker a API pode não conseguir ler a role da máquina por causa de um limite de "saltos" de rede (IMDSv2).
> Se o log disser que não achou credenciais: EC2 → instância → **Actions** → **Instance settings** →
> **Modify instance metadata options** → **Metadata response hop limit = 2**.
