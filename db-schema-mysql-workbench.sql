-- ============================================================
-- Comments SPA — DB Schema (MySQL Workbench compatible)
-- ============================================================
-- Purpose : open in MySQL Workbench to visualize the schema design.
-- Note    : production database runs on PostgreSQL 17.
--           Column names, types and constraints are equivalent;
--           only dialect-specific syntax differs (CHAR(36) instead
--           of uuid, DATETIME instead of timestamptz, etc.).
-- ============================================================

CREATE TABLE IF NOT EXISTS `Comments` (
    `Id`              CHAR(36)        NOT NULL,
    `UserName`        VARCHAR(64)     NOT NULL,
    `Email`           VARCHAR(255)    NOT NULL,
    `HomePage`        VARCHAR(255)    NULL,
    `Text`            VARCHAR(10000)  NOT NULL,
    `IpAddress`       VARCHAR(64)     NOT NULL,
    `CreatedAtUtc`    DATETIME        NOT NULL,
    `ParentCommentId` CHAR(36)        NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Comments_Comments_ParentCommentId`
        FOREIGN KEY (`ParentCommentId`)
        REFERENCES `Comments` (`Id`)
        ON DELETE CASCADE,
    INDEX `IX_Comments_CreatedAtUtc` (`CreatedAtUtc`),
    INDEX `IX_Comments_Email`        (`Email`),
    INDEX `IX_Comments_UserName`     (`UserName`),
    INDEX `IX_Comments_ParentCommentId` (`ParentCommentId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- AttachmentType: Image = 1, Text = 2
CREATE TABLE IF NOT EXISTS `CommentAttachments` (
    `Id`               CHAR(36)        NOT NULL,
    `CommentId`        CHAR(36)        NOT NULL,
    `OriginalFileName` VARCHAR(260)    NOT NULL,
    `StoredFileName`   VARCHAR(260)    NOT NULL,
    `ContentType`      VARCHAR(100)    NOT NULL,
    `Type`             INT             NOT NULL,
    `SizeBytes`        BIGINT          NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CommentAttachments_Comments_CommentId`
        FOREIGN KEY (`CommentId`)
        REFERENCES `Comments` (`Id`)
        ON DELETE CASCADE,
    INDEX `IX_CommentAttachments_CommentId` (`CommentId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
