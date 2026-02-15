# Phase 3: Flask Backend Setup Guide

## Prerequisites

- Python 3.10+ installed
- PostgreSQL installed and running

## Step 1: Set Up PostgreSQL

Open a terminal (or pgAdmin) and create the database:

```sql
-- In psql or pgAdmin:
CREATE USER naruto WITH PASSWORD 'naruto';
CREATE DATABASE naruto_tactics OWNER naruto;
GRANT ALL PRIVILEGES ON DATABASE naruto_tactics TO naruto;
```

Or from command line:
```bash
psql -U postgres -c "CREATE USER naruto WITH PASSWORD 'naruto';"
psql -U postgres -c "CREATE DATABASE naruto_tactics OWNER naruto;"
```

## Step 2: Set Up Python Environment

```bash
cd backend
python -m venv venv

# Windows:
venv\Scripts\activate

# Mac/Linux:
source venv/bin/activate

pip install -r requirements.txt
```

## Step 3: Configure Environment

```bash
copy .env.example .env
```

Edit `.env` with your settings. The defaults work for local dev.

## Step 4: Run the Server

```bash
python app.py
```

You should see:
```
 * Running on http://0.0.0.0:5000
 * Debug mode: on
```

## Step 5: Test the API

Health check:
```bash
curl http://localhost:5000/api/health
```

Register an account:
```bash
curl -X POST http://localhost:5000/api/auth/register ^
  -H "Content-Type: application/json" ^
  -d "{\"username\": \"test\", \"email\": \"test@test.com\", \"password\": \"password123\"}"
```

## API Reference

### Auth
| Method | Endpoint                  | Body                                      | Auth? |
|--------|---------------------------|-------------------------------------------|-------|
| POST   | /api/auth/register        | username, email, password                 | No    |
| POST   | /api/auth/login           | username, password                        | No    |
| GET    | /api/auth/me              | -                                         | Yes   |
| POST   | /api/auth/change-password | old_password, new_password                | Yes   |

### Characters
| Method | Endpoint                            | Body/Params                        | Auth? |
|--------|-------------------------------------|------------------------------------|-------|
| GET    | /api/characters/                    | -                                  | Yes   |
| POST   | /api/characters/                    | name, village, bio, slot (1-3)     | Yes   |
| GET    | /api/characters/{id}                | -                                  | Yes   |
| PUT    | /api/characters/{id}                | bio, current_hp, current_chakra    | Yes   |
| DELETE | /api/characters/{id}                | -                                  | Yes   |
| POST   | /api/characters/{id}/train          | stat, points                       | Yes   |
| GET    | /api/characters/check-name?name=X   | -                                  | Yes   |

### Admin (requires admin account)
| Method | Endpoint                                | Body                    | Auth?  |
|--------|-----------------------------------------|-------------------------|--------|
| GET    | /api/admin/accounts                     | -                       | Admin  |
| GET    | /api/admin/characters                   | -                       | Admin  |
| POST   | /api/admin/character/{id}/set-rank      | rank                    | Admin  |
| POST   | /api/admin/character/{id}/grant-stats   | grants: {stat: amount}  | Admin  |
| POST   | /api/admin/account/{id}/ban             | ban: true/false         | Admin  |

### All authenticated requests need:
```
Authorization: Bearer <your-jwt-token>
```

## File Structure

```
backend/
├── app.py              ← Flask app factory, entry point
├── database.py         ← SQLAlchemy db instance
├── models.py           ← Account + Character models
├── requirements.txt    ← Python dependencies
├── .env.example        ← Environment template
├── .env                ← Your local config (don't commit)
└── routes/
    ├── __init__.py
    ├── auth.py         ← Register, login, JWT
    ├── characters.py   ← Character CRUD + training
    └── admin.py        ← Staff moderation tools
```

## What's Server-Authoritative

The backend validates and enforces:
- **Stat training**: Soft cap diminishing returns calculated server-side
- **Daily points**: Server tracks remaining points and last training date
- **Character limits**: Max 3 per account, unique names
- **Name uniqueness**: Checked against all characters in the database
- **Rank changes**: Admin-only (no self-promotion)
- **HP/Chakra caps**: Can't exceed max derived values
