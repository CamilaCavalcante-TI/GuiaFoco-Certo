-- GuiaFoco | Esquema MySQL 8
-- Use este script SOMENTE se preferir criar o banco manualmente.
-- Nesse caso defina "Database:AutoCreate": false no appsettings.json.
-- (Com AutoCreate = true a própria API cria as tabelas no primeiro start.)

CREATE DATABASE IF NOT EXISTS guiafoco
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE guiafoco;

CREATE TABLE IF NOT EXISTS responsaveis (
  id          INT          NOT NULL AUTO_INCREMENT,
  nome        VARCHAR(120) NOT NULL,
  email       VARCHAR(180) NOT NULL,
  senha_hash  VARCHAR(100) NOT NULL,
  criado_em   DATETIME(6)  NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY ix_responsaveis_email (email)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS criancas (
  id                          INT          NOT NULL AUTO_INCREMENT,
  responsavel_id              INT          NOT NULL,
  nome                        VARCHAR(120) NOT NULL,
  idade                       INT          NOT NULL,
  serie                       VARCHAR(60)  NOT NULL,
  tempo_tela_estimado_horas   DECIMAL(4,1) NOT NULL,
  pontos                      INT          NOT NULL DEFAULT 0,
  criado_em                   DATETIME(6)  NOT NULL,
  PRIMARY KEY (id),
  KEY ix_criancas_responsavel_id (responsavel_id),
  CONSTRAINT fk_criancas_responsaveis FOREIGN KEY (responsavel_id)
    REFERENCES responsaveis (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS configuracoes_crianca (
  crianca_id              INT          NOT NULL,
  limite_diario_min       INT          NOT NULL,
  inicio_permitido        VARCHAR(5)   NOT NULL,
  fim_permitido           VARCHAR(5)   NOT NULL,
  preferencias            VARCHAR(200) NOT NULL,
  intervencao_adaptativa  TINYINT(1)   NOT NULL,
  intervir_apos_min       INT          NOT NULL,
  bloqueio_simulado       TINYINT(1)   NOT NULL,
  alto_contraste          TINYINT(1)   NOT NULL,
  modo_escuro             TINYINT(1)   NOT NULL,
  fonte_percentual        INT          NOT NULL,
  PRIMARY KEY (crianca_id),
  CONSTRAINT fk_config_criancas FOREIGN KEY (crianca_id)
    REFERENCES criancas (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS sessoes_uso (
  id                     INT         NOT NULL AUTO_INCREMENT,
  crianca_id             INT         NOT NULL,
  inicio_em              DATETIME(6) NOT NULL,
  fim_em                 DATETIME(6) NULL,
  abandonada             TINYINT(1)  NOT NULL,
  intervencoes_exibidas  INT         NOT NULL,
  PRIMARY KEY (id),
  KEY ix_sessoes_crianca_inicio (crianca_id, inicio_em),
  CONSTRAINT fk_sessoes_criancas FOREIGN KEY (crianca_id)
    REFERENCES criancas (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS desafios (
  id          INT          NOT NULL AUTO_INCREMENT,
  tipo        VARCHAR(30)  NOT NULL,
  titulo      VARCHAR(80)  NOT NULL,
  descricao   VARCHAR(250) NOT NULL,
  emoji       VARCHAR(16)  NOT NULL,
  disponivel  TINYINT(1)   NOT NULL,
  ordem       INT          NOT NULL,
  PRIMARY KEY (id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS perguntas (
  id          INT          NOT NULL AUTO_INCREMENT,
  desafio_id  INT          NOT NULL,
  enunciado   VARCHAR(250) NOT NULL,
  pontos      INT          NOT NULL,
  PRIMARY KEY (id),
  KEY ix_perguntas_desafio_id (desafio_id),
  CONSTRAINT fk_perguntas_desafios FOREIGN KEY (desafio_id)
    REFERENCES desafios (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS opcoes_resposta (
  id           INT         NOT NULL AUTO_INCREMENT,
  pergunta_id  INT         NOT NULL,
  texto        VARCHAR(80) NOT NULL,
  emoji        VARCHAR(16) NULL,
  correta      TINYINT(1)  NOT NULL,
  PRIMARY KEY (id),
  KEY ix_opcoes_pergunta_id (pergunta_id),
  CONSTRAINT fk_opcoes_perguntas FOREIGN KEY (pergunta_id)
    REFERENCES perguntas (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS respostas_crianca (
  id                INT         NOT NULL AUTO_INCREMENT,
  crianca_id        INT         NOT NULL,
  pergunta_id       INT         NOT NULL,
  opcao_id          INT         NOT NULL,
  sessao_id         INT         NULL,
  acertou           TINYINT(1)  NOT NULL,
  tentativa         INT         NOT NULL,
  tempo_resposta_ms INT         NOT NULL,
  pontos_ganhos     INT         NOT NULL,
  respondido_em     DATETIME(6) NOT NULL,
  PRIMARY KEY (id),
  KEY ix_respostas_crianca_data (crianca_id, respondido_em),
  KEY ix_respostas_pergunta_id (pergunta_id),
  KEY ix_respostas_opcao_id (opcao_id),
  KEY ix_respostas_sessao_id (sessao_id),
  CONSTRAINT fk_respostas_criancas  FOREIGN KEY (crianca_id)  REFERENCES criancas (id) ON DELETE CASCADE,
  CONSTRAINT fk_respostas_perguntas FOREIGN KEY (pergunta_id) REFERENCES perguntas (id),
  CONSTRAINT fk_respostas_opcoes    FOREIGN KEY (opcao_id)    REFERENCES opcoes_resposta (id),
  CONSTRAINT fk_respostas_sessoes   FOREIGN KEY (sessao_id)   REFERENCES sessoes_uso (id) ON DELETE SET NULL
) ENGINE=InnoDB;
