-- PostgreSQL schema for Comments SPA
-- Generated from EF Core migration (PascalCase naming convention used by default)
-- Open in pgAdmin, DBeaver, or any PostgreSQL-compatible client.

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId"    varchar(150) NOT NULL,
    "ProductVersion" varchar(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE IF NOT EXISTS "Comments" (
    "Id"              uuid                        NOT NULL,
    "UserName"        character varying(64)       NOT NULL,
    "Email"           character varying(255)      NOT NULL,
    "HomePage"        character varying(255)      NULL,
    "Text"            character varying(10000)    NOT NULL,
    "IpAddress"       character varying(64)       NOT NULL,
    "CreatedAtUtc"    timestamp with time zone    NOT NULL,
    "ParentCommentId" uuid                        NULL,
    CONSTRAINT "PK_Comments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Comments_Comments_ParentCommentId"
        FOREIGN KEY ("ParentCommentId")
        REFERENCES "Comments" ("Id")
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_Comments_CreatedAtUtc"    ON "Comments" ("CreatedAtUtc");
CREATE INDEX IF NOT EXISTS "IX_Comments_Email"           ON "Comments" ("Email");
CREATE INDEX IF NOT EXISTS "IX_Comments_UserName"        ON "Comments" ("UserName");
CREATE INDEX IF NOT EXISTS "IX_Comments_ParentCommentId" ON "Comments" ("ParentCommentId");

CREATE TABLE IF NOT EXISTS "CommentAttachments" (
    "Id"               uuid                    NOT NULL,
    "CommentId"        uuid                    NOT NULL,
    "OriginalFileName" character varying(260)  NOT NULL,
    "StoredFileName"   character varying(260)  NOT NULL,
    "ContentType"      character varying(100)  NOT NULL,
    "Type"             integer                 NOT NULL,
    "SizeBytes"        bigint                  NOT NULL,
    CONSTRAINT "PK_CommentAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CommentAttachments_Comments_CommentId"
        FOREIGN KEY ("CommentId")
        REFERENCES "Comments" ("Id")
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_CommentAttachments_CommentId" ON "CommentAttachments" ("CommentId");

-- AttachmentType enum values: Image = 1, Text = 2
