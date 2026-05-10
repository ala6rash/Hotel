# SecureDocs — Secure Document Delivery Platform

A lean MVP SaaS for sharing documents securely with full post-send control (revoke, expire, track).

## Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 14 (App Router) + TypeScript |
| Styling | Tailwind CSS |
| Database | PostgreSQL via Prisma ORM |
| Auth | NextAuth.js (email/password) |
| File Storage | AWS S3 (presigned URLs) |
| Email | Resend |
| Hosting | Vercel (recommended) |

## Features

- **User registration & login** – email/password, password reset via email
- **File upload** – drag-and-drop, up to 100MB, direct-to-S3 via presigned URL
- **Secure share links** – unique unguessable token, no login required for recipients
- **Expiry controls** – never / after X days / view-once
- **Instant revoke** – sender can revoke any share immediately
- **Recipient view** – PDF preview, image viewer, download button
- **Email notifications** – send link to recipient via Resend
- **Dashboard** – view all files, shares, access stats (view count, last viewed)

## Project Structure

```
src/
├── app/
│   ├── page.tsx                  # Landing page
│   ├── (auth)/                   # Login, register, forgot-password
│   ├── (dashboard)/              # Dashboard, upload — protected
│   │   ├── dashboard/page.tsx
│   │   └── upload/page.tsx
│   ├── d/[token]/page.tsx        # Public recipient view
│   └── api/
│       ├── auth/                 # NextAuth + register + forgot-password
│       ├── upload/presigned/     # S3 presigned URL generation
│       ├── files/[fileId]/       # File management + revoke
│       ├── share/                # Create share links
│       ├── doc/[token]/          # Access doc + download
│       └── stats/                # Dashboard stats
├── components/
│   ├── layout/                   # Sidebar, header
│   └── dashboard/                # FileTable, StatsCards, EmptyState
└── lib/
    ├── auth.ts                   # NextAuth config
    ├── prisma.ts                 # Prisma client singleton
    ├── s3.ts                     # AWS S3 helpers
    ├── email.ts                  # Resend email templates
    └── utils.ts                  # Shared utilities
```

## Setup

### 1. Clone & install

```bash
git clone <repo-url>
cd secure-doc-delivery
npm install
```

### 2. Configure environment variables

```bash
cp .env.example .env
```

Fill in all values in `.env`:

```env
DATABASE_URL="postgresql://user:pass@host:5432/secure_docs"
NEXTAUTH_URL="https://yourdomain.com"
NEXTAUTH_SECRET="<openssl rand -base64 32>"

AWS_ACCESS_KEY_ID="..."
AWS_SECRET_ACCESS_KEY="..."
AWS_REGION="us-east-1"
AWS_S3_BUCKET="your-bucket-name"

RESEND_API_KEY="re_..."
EMAIL_FROM="noreply@yourdomain.com"

NEXT_PUBLIC_APP_URL="https://yourdomain.com"
NEXT_PUBLIC_APP_NAME="SecureDocs"
```

### 3. Set up the database

```bash
npm run db:push      # push schema to DB
npm run db:generate  # generate Prisma client
```

### 4. Run locally

```bash
npm run dev
```

Visit `http://localhost:3000`

## AWS S3 Setup

1. Create an S3 bucket (e.g. `my-secure-docs`)
2. Block all public access
3. Add a CORS configuration:

```json
[
  {
    "AllowedHeaders": ["*"],
    "AllowedMethods": ["GET", "PUT", "POST", "DELETE", "HEAD"],
    "AllowedOrigins": ["https://yourdomain.com"],
    "ExposeHeaders": ["ETag"]
  }
]
```

4. Create an IAM user with `AmazonS3FullAccess` (or a scoped policy) and add the credentials to `.env`

## Deployment (Vercel + Neon PostgreSQL)

### Database (Neon — recommended free tier)

1. Create account at neon.tech
2. Create a new project / database
3. Copy the connection string to `DATABASE_URL`

### Vercel

```bash
npm install -g vercel
vercel login
vercel --prod
```

Set all environment variables in the Vercel dashboard under Project → Settings → Environment Variables.

After deploying:

```bash
# Run migrations against production DB
DATABASE_URL=<prod-url> npx prisma db push
```

## Key API Reference

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/register` | Register new user |
| POST | `/api/upload/presigned` | Get S3 presigned upload URL |
| GET | `/api/files` | List sender's files |
| DELETE | `/api/files/:id` | Delete file |
| POST | `/api/files/:id/revoke` | Revoke one or all shares for a file |
| POST | `/api/share` | Create a share link |
| GET | `/api/doc/:token` | Access a shared document (logs view) |
| GET | `/api/doc/:token/download` | Download shared file |
| GET | `/api/stats` | Dashboard stats |

## Security Notes

- All files stored in private S3 bucket; accessed only via short-lived presigned URLs (5 min)
- Share tokens are CUID-based (unguessable, collision-resistant)
- Passwords hashed with bcrypt (cost 12)
- Revocation is instant — the API checks `status === ACTIVE` on every access
- Expiry enforced server-side on every doc access request
- HTTPS enforced via Vercel/deployment platform
