USE CandidateAccountingDb;
GO

/* 
    4.1.2 Хранимые процедуры и триггеры.
    Скрипт можно выполнить в SQL Server Management Studio после создания основных таблиц.
*/

IF OBJECT_ID(N'dbo.CardChangeLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CardChangeLog (
        LogId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CardChangeLog PRIMARY KEY,
        CardId      INT NOT NULL,
        ActionName  NVARCHAR(30) NOT NULL,
        OldStage    NVARCHAR(50) NULL,
        NewStage    NVARCHAR(50) NULL,
        ChangedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_CardChangeLog_ChangedAt DEFAULT SYSDATETIME()
    );
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_CreateProcessingCard
    @FullName        NVARCHAR(255),
    @BirthDate       DATE,
    @Phone           NVARCHAR(30) = NULL,
    @Email           NVARCHAR(150) = NULL,
    @EducationLevel  NVARCHAR(100),
    @EducationType   NVARCHAR(150) = NULL,
    @Institution     NVARCHAR(255) = NULL,
    @SourceId        INT,
    @VacancyId       INT,
    @HrUserId        INT,
    @StatementDate   DATE,
    @OtherInfo       NVARCHAR(MAX) = NULL,
    @CardId          INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @CandidateId INT;

        INSERT INTO dbo.Candidates (FullName, BirthDate, Phone, Email)
        VALUES (@FullName, @BirthDate, @Phone, @Email);

        SET @CandidateId = SCOPE_IDENTITY();

        INSERT INTO dbo.Education (CandidateId, EducationLevel, EducationType, Institution, DocumentInfo)
        VALUES (@CandidateId, @EducationLevel, @EducationType, @Institution, NULL);

        INSERT INTO dbo.ProcessingCards
            (CandidateId, SourceId, VacancyId, HrUserId, StatementDate, StudyStage, OtherInfo)
        VALUES
            (@CandidateId, @SourceId, @VacancyId, @HrUserId, @StatementDate, N'На оформлении', @OtherInfo);

        SET @CardId = SCOPE_IDENTITY();

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SaveStudyStage
    @CardId          INT,
    @StageTypeId     INT,
    @DirectionDate   DATE = NULL,
    @ResultDate      DATE = NULL,
    @ResultText      NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.StudyStages WHERE CardId = @CardId AND StageTypeId = @StageTypeId)
    BEGIN
        UPDATE dbo.StudyStages
        SET DirectionDate = @DirectionDate,
            ResultDate = @ResultDate,
            ResultText = @ResultText
        WHERE CardId = @CardId
          AND StageTypeId = @StageTypeId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.StudyStages (CardId, StageTypeId, DirectionDate, ResultDate, ResultText)
        VALUES (@CardId, @StageTypeId, @DirectionDate, @ResultDate, @ResultText);
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SaveServiceCheck
    @CardId          INT,
    @CheckTypeId     INT,
    @DirectionDate   DATE = NULL,
    @ResultDate      DATE = NULL,
    @ResultText      NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.ServiceChecks WHERE CardId = @CardId AND CheckTypeId = @CheckTypeId)
    BEGIN
        UPDATE dbo.ServiceChecks
        SET DirectionDate = @DirectionDate,
            ResultDate = @ResultDate,
            ResultText = @ResultText
        WHERE CardId = @CardId
          AND CheckTypeId = @CheckTypeId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.ServiceChecks (CardId, CheckTypeId, DirectionDate, ResultDate, ResultText)
        VALUES (@CardId, @CheckTypeId, @DirectionDate, @ResultDate, @ResultText);
    END
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_SaveDecision
    @CardId          INT,
    @DecisionType    NVARCHAR(30),
    @DecisionDate    DATE = NULL,
    @RefusalReason   NVARCHAR(MAX) = NULL,
    @OrderDetails    NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @DecisionType = N'Отказ' AND NULLIF(LTRIM(RTRIM(@RefusalReason)), N'') IS NULL
    BEGIN
        THROW 50001, N'Для отказа необходимо указать причину.', 1;
    END

    IF @DecisionType = N'Принят' AND NULLIF(LTRIM(RTRIM(@OrderDetails)), N'') IS NULL
    BEGIN
        THROW 50002, N'Для принятого кандидата необходимо указать реквизиты приказа.', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.Decisions WHERE CardId = @CardId)
    BEGIN
        UPDATE dbo.Decisions
        SET DecisionType = @DecisionType,
            DecisionDate = @DecisionDate,
            RefusalReason = @RefusalReason,
            OrderDetails = @OrderDetails
        WHERE CardId = @CardId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.Decisions (CardId, DecisionType, DecisionDate, RefusalReason, OrderDetails)
        VALUES (@CardId, @DecisionType, @DecisionDate, @RefusalReason, @OrderDetails);
    END
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Decisions_UpdateProcessingCardStage
ON dbo.Decisions
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE pc
    SET StudyStage =
        CASE i.DecisionType
            WHEN N'Принят' THEN N'Принят на службу'
            WHEN N'Отказ' THEN N'Отказ'
            ELSE N'На оформлении'
        END
    FROM dbo.ProcessingCards pc
    INNER JOIN inserted i ON i.CardId = pc.CardId;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_ProcessingCards_LogStageChange
ON dbo.ProcessingCards
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.CardChangeLog (CardId, ActionName, OldStage, NewStage)
    SELECT
        i.CardId,
        CASE WHEN d.CardId IS NULL THEN N'Создание' ELSE N'Изменение' END,
        d.StudyStage,
        i.StudyStage
    FROM inserted i
    LEFT JOIN deleted d ON d.CardId = i.CardId
    WHERE d.CardId IS NULL
       OR ISNULL(d.StudyStage, N'') <> ISNULL(i.StudyStage, N'');
END
GO
