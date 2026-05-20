-- ================================================================
--  Инициализация баз данных PostgreSQL
--  Файл выполняется автоматически при первом запуске контейнера
--  Путь в docker-compose: ./infra/postgres/init.sql
-- ================================================================

-- Создаём отдельные базы данных для каждого сервиса
CREATE DATABASE auth_db;
CREATE DATABASE puzzle_db;

-- ----------------------------------------------------------------
--  AUTH DB
-- ----------------------------------------------------------------

\connect auth_db;

CREATE TABLE IF NOT EXISTS users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email         VARCHAR(255) NOT NULL UNIQUE,
    username      VARCHAR(100) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,  -- bcrypt hash
    created_at    TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_email ON users(email);

-- ----------------------------------------------------------------
--  PUZZLE DB
-- ----------------------------------------------------------------

\connect puzzle_db;

CREATE TABLE IF NOT EXISTS puzzles (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    open_part   TEXT NOT NULL,             -- видит пользователь
    hidden_part TEXT NOT NULL,             -- никогда не отдаётся клиенту
    source_url  VARCHAR(2048) NOT NULL UNIQUE, -- дедупликация по URL
    story_id    VARCHAR(255),              -- ID истории из MongoDB
    created_at  TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_puzzles_created_at ON puzzles(created_at);
CREATE UNIQUE INDEX idx_puzzles_source_url ON puzzles(source_url);
