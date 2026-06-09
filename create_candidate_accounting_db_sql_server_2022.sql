CREATE DATABASE CandidateAccountingDb;
GO

USE CandidateAccountingDb;
GO

CREATE TABLE dbo.Users (
    UserId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    FullName        NVARCHAR(255) NOT NULL,
    Login           NVARCHAR(100) NOT NULL,
    PasswordHash    NVARCHAR(255) NOT NULL,
    RoleName        NVARCHAR(30) NOT NULL,
    CONSTRAINT UQ_Users_Login UNIQUE (Login),
    CONSTRAINT CK_Users_RoleName CHECK (RoleName IN (N'Кадровик', N'Администратор'))
);
GO

CREATE TABLE dbo.Candidates (
    CandidateId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Candidates PRIMARY KEY,
    FullName        NVARCHAR(255) NOT NULL,
    BirthDate       DATE NOT NULL,
    Phone           NVARCHAR(30) NULL,
    Email           NVARCHAR(150) NULL
);
GO

CREATE TABLE dbo.Education (
    EducationId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Education PRIMARY KEY,
    CandidateId     INT NOT NULL,
    EducationLevel  NVARCHAR(100) NOT NULL,
    EducationType   NVARCHAR(150) NULL,
    Institution     NVARCHAR(255) NULL,
    DocumentInfo    NVARCHAR(255) NULL,
    CONSTRAINT FK_Education_Candidates
        FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates(CandidateId)
        ON DELETE CASCADE
);
GO

CREATE TABLE dbo.InformationSources (
    SourceId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InformationSources PRIMARY KEY,
    SourceName      NVARCHAR(255) NOT NULL,
    CONSTRAINT UQ_InformationSources_SourceName UNIQUE (SourceName)
);
GO

CREATE TABLE dbo.ServiceCategories (
    CategoryId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceCategories PRIMARY KEY,
    CategoryName    NVARCHAR(100) NOT NULL,
    CONSTRAINT UQ_ServiceCategories_CategoryName UNIQUE (CategoryName)
);
GO

CREATE TABLE dbo.Departments (
    DepartmentId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Departments PRIMARY KEY,
    DepartmentName      NVARCHAR(255) NOT NULL,
    DepartmentType      NVARCHAR(100) NULL,
    ParentDepartmentId  INT NULL,
    CONSTRAINT FK_Departments_ParentDepartment
        FOREIGN KEY (ParentDepartmentId) REFERENCES dbo.Departments(DepartmentId)
);
GO

CREATE TABLE dbo.Services (
    ServiceId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Services PRIMARY KEY,
    ServiceName     NVARCHAR(150) NOT NULL,
    CONSTRAINT UQ_Services_ServiceName UNIQUE (ServiceName)
);
GO

CREATE TABLE dbo.Positions (
    PositionId              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Positions PRIMARY KEY,
    PositionName            NVARCHAR(255) NOT NULL,
    CommandStaffLevel       NVARCHAR(100) NULL
);
GO

CREATE TABLE dbo.Vacancies (
    VacancyId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vacancies PRIMARY KEY,
    CategoryId      INT NOT NULL,
    DepartmentId    INT NOT NULL,
    ServiceId       INT NOT NULL,
    PositionId      INT NOT NULL,
    CONSTRAINT FK_Vacancies_ServiceCategories
        FOREIGN KEY (CategoryId) REFERENCES dbo.ServiceCategories(CategoryId),
    CONSTRAINT FK_Vacancies_Departments
        FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(DepartmentId),
    CONSTRAINT FK_Vacancies_Services
        FOREIGN KEY (ServiceId) REFERENCES dbo.Services(ServiceId),
    CONSTRAINT FK_Vacancies_Positions
        FOREIGN KEY (PositionId) REFERENCES dbo.Positions(PositionId),
    CONSTRAINT UQ_Vacancies_Link UNIQUE (CategoryId, DepartmentId, ServiceId, PositionId)
);
GO

CREATE TABLE dbo.ProcessingCards (
    CardId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProcessingCards PRIMARY KEY,
    CandidateId     INT NOT NULL,
    SourceId        INT NOT NULL,
    VacancyId       INT NOT NULL,
    HrUserId        INT NOT NULL,
    StatementDate   DATE NOT NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_ProcessingCards_CreatedAt DEFAULT SYSDATETIME(),
    StudyStage      NVARCHAR(50) NOT NULL,
    OtherInfo       NVARCHAR(MAX) NULL,
    CONSTRAINT FK_ProcessingCards_Candidates
        FOREIGN KEY (CandidateId) REFERENCES dbo.Candidates(CandidateId),
    CONSTRAINT FK_ProcessingCards_InformationSources
        FOREIGN KEY (SourceId) REFERENCES dbo.InformationSources(SourceId),
    CONSTRAINT FK_ProcessingCards_Vacancies
        FOREIGN KEY (VacancyId) REFERENCES dbo.Vacancies(VacancyId),
    CONSTRAINT FK_ProcessingCards_Users
        FOREIGN KEY (HrUserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_ProcessingCards_StudyStage
        CHECK (StudyStage IN (N'На оформлении', N'Принят на службу', N'Отказ'))
);
GO

CREATE TABLE dbo.StudyStageTypes (
    StageTypeId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudyStageTypes PRIMARY KEY,
    StageTypeName   NVARCHAR(150) NOT NULL,
    CONSTRAINT UQ_StudyStageTypes_StageTypeName UNIQUE (StageTypeName)
);
GO

CREATE TABLE dbo.StudyStages (
    StageId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StudyStages PRIMARY KEY,
    CardId          INT NOT NULL,
    StageTypeId     INT NOT NULL,
    DirectionDate   DATE NULL,
    ResultDate      DATE NULL,
    ResultText      NVARCHAR(MAX) NULL,
    CONSTRAINT FK_StudyStages_ProcessingCards
        FOREIGN KEY (CardId) REFERENCES dbo.ProcessingCards(CardId)
        ON DELETE CASCADE,
    CONSTRAINT FK_StudyStages_StudyStageTypes
        FOREIGN KEY (StageTypeId) REFERENCES dbo.StudyStageTypes(StageTypeId),
    CONSTRAINT UQ_StudyStages_Card_StageType UNIQUE (CardId, StageTypeId)
);
GO

CREATE TABLE dbo.CheckTypes (
    CheckTypeId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CheckTypes PRIMARY KEY,
    CheckTypeName   NVARCHAR(150) NOT NULL,
    CONSTRAINT UQ_CheckTypes_CheckTypeName UNIQUE (CheckTypeName)
);
GO

CREATE TABLE dbo.ServiceChecks (
    CheckId         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ServiceChecks PRIMARY KEY,
    CardId          INT NOT NULL,
    CheckTypeId     INT NOT NULL,
    DirectionDate   DATE NULL,
    ResultDate      DATE NULL,
    ResultText      NVARCHAR(MAX) NULL,
    CONSTRAINT FK_ServiceChecks_ProcessingCards
        FOREIGN KEY (CardId) REFERENCES dbo.ProcessingCards(CardId)
        ON DELETE CASCADE,
    CONSTRAINT FK_ServiceChecks_CheckTypes
        FOREIGN KEY (CheckTypeId) REFERENCES dbo.CheckTypes(CheckTypeId),
    CONSTRAINT UQ_ServiceChecks_Card_CheckType UNIQUE (CardId, CheckTypeId)
);
GO

CREATE TABLE dbo.Decisions (
    DecisionId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Decisions PRIMARY KEY,
    CardId          INT NOT NULL,
    DecisionType    NVARCHAR(30) NOT NULL,
    DecisionDate    DATE NULL,
    RefusalReason   NVARCHAR(MAX) NULL,
    OrderDetails    NVARCHAR(MAX) NULL,
    CONSTRAINT FK_Decisions_ProcessingCards
        FOREIGN KEY (CardId) REFERENCES dbo.ProcessingCards(CardId)
        ON DELETE CASCADE,
    CONSTRAINT UQ_Decisions_CardId UNIQUE (CardId),
    CONSTRAINT CK_Decisions_DecisionType
        CHECK (DecisionType IN (N'Принят', N'Отказ', N'На оформлении')),
    CONSTRAINT CK_Decisions_RefusalReason
        CHECK (DecisionType <> N'Отказ' OR RefusalReason IS NOT NULL),
    CONSTRAINT CK_Decisions_OrderDetails
        CHECK (DecisionType <> N'Принят' OR OrderDetails IS NOT NULL)
);
GO

CREATE TABLE dbo.ReportTemplates (
    TemplateId      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportTemplates PRIMARY KEY,
    TemplateName    NVARCHAR(255) NOT NULL,
    ReportTitle     NVARCHAR(500) NOT NULL,
    CONSTRAINT UQ_ReportTemplates_TemplateName UNIQUE (TemplateName)
);
GO

CREATE TABLE dbo.ReportTemplateColumns (
    TemplateColumnId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportTemplateColumns PRIMARY KEY,
    TemplateId          INT NOT NULL,
    ExcelColumnNumber   INT NOT NULL,
    ColumnTitle         NVARCHAR(255) NOT NULL,
    DataSource          NVARCHAR(255) NOT NULL,
    CONSTRAINT FK_ReportTemplateColumns_ReportTemplates
        FOREIGN KEY (TemplateId) REFERENCES dbo.ReportTemplates(TemplateId)
        ON DELETE CASCADE,
    CONSTRAINT CK_ReportTemplateColumns_ExcelColumnNumber
        CHECK (ExcelColumnNumber > 0),
    CONSTRAINT UQ_ReportTemplateColumns_Template_Column UNIQUE (TemplateId, ExcelColumnNumber)
);
GO

CREATE TABLE dbo.ReportExports (
    ExportId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportExports PRIMARY KEY,
    TemplateId      INT NOT NULL,
    UserId          INT NOT NULL,
    ReportMonth     TINYINT NOT NULL,
    ReportYear      SMALLINT NOT NULL,
    FileName        NVARCHAR(255) NOT NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_ReportExports_CreatedAt DEFAULT SYSDATETIME(),
    CONSTRAINT FK_ReportExports_ReportTemplates
        FOREIGN KEY (TemplateId) REFERENCES dbo.ReportTemplates(TemplateId),
    CONSTRAINT FK_ReportExports_Users
        FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_ReportExports_ReportMonth CHECK (ReportMonth BETWEEN 1 AND 12),
    CONSTRAINT CK_ReportExports_ReportYear CHECK (ReportYear >= 2000)
);
GO

CREATE INDEX IX_Candidates_FullName
    ON dbo.Candidates(FullName);
GO

CREATE INDEX IX_ProcessingCards_CandidateId
    ON dbo.ProcessingCards(CandidateId);
GO

CREATE INDEX IX_ProcessingCards_StatementDate
    ON dbo.ProcessingCards(StatementDate);
GO

CREATE INDEX IX_ProcessingCards_StudyStage
    ON dbo.ProcessingCards(StudyStage);
GO

CREATE INDEX IX_StudyStages_CardId
    ON dbo.StudyStages(CardId);
GO

CREATE INDEX IX_ServiceChecks_CardId
    ON dbo.ServiceChecks(CardId);
GO

CREATE INDEX IX_ReportExports_Period
    ON dbo.ReportExports(ReportYear, ReportMonth);
GO

INSERT INTO dbo.ServiceCategories (CategoryName)
VALUES (N'Юстиция'), (N'Полиция'), (N'Внутренняя служба');
GO

INSERT INTO dbo.InformationSources (SourceName)
VALUES
    (N'Рекомендация сотрудника'),
    (N'Сайт МВД'),
    (N'Работа в России'),
    (N'Социальные сети'),
    (N'Объявление'),
    (N'Иное');
GO

INSERT INTO dbo.StudyStageTypes (StageTypeName)
VALUES
    (N'Утверждение задания для изучения кандидата'),
    (N'Направление установок УП/УР'),
    (N'Испытания'),
    (N'ВВК'),
    (N'ППО');
GO

INSERT INTO dbo.CheckTypes (CheckTypeName)
VALUES
    (N'ИЦ'),
    (N'ГИАЦ'),
    (N'ОРЧ СБ'),
    (N'ФСБ'),
    (N'ГИБДД'),
    (N'БСТМ'),
    (N'СООП'),
    (N'УВМ'),
    (N'Проверка диплома'),
    (N'Допуск к СГТ');
GO

INSERT INTO dbo.ReportTemplates (TemplateName, ReportTitle)
VALUES (
    N'Ежемесячный отчет по кандидатам',
    N'МОНИТОРИНГ РАБОТЫ С КАНДИДАТАМИ ДЛЯ ПРИЕМА ИХ НА СЛУЖБУ'
);
GO

DECLARE @TemplateId INT = SCOPE_IDENTITY();

INSERT INTO dbo.ReportTemplateColumns (TemplateId, ExcelColumnNumber, ColumnTitle, DataSource)
VALUES
    (@TemplateId, 1,  N'№ п/п', N'Generated.RowNumber'),
    (@TemplateId, 2,  N'дата заявления', N'ProcessingCards.StatementDate'),
    (@TemplateId, 3,  N'стадия изучения на 20 число', N'ProcessingCards.StudyStage'),
    (@TemplateId, 4,  N'ФИО кандидата', N'Candidates.FullName'),
    (@TemplateId, 5,  N'дата рождения кандидата', N'Candidates.BirthDate'),
    (@TemplateId, 6,  N'уровень и вид образования кандидата', N'Education.EducationLevel + Education.EducationType'),
    (@TemplateId, 7,  N'источник получения сведений', N'InformationSources.SourceName'),
    (@TemplateId, 8,  N'подразделение МВД, территориальный орган МВД', N'Departments.DepartmentName'),
    (@TemplateId, 9,  N'начальствующий состав', N'Positions.CommandStaffLevel'),
    (@TemplateId, 10, N'категория', N'ServiceCategories.CategoryName'),
    (@TemplateId, 11, N'наименование должности', N'Positions.PositionName'),
    (@TemplateId, 12, N'наименование службы', N'Services.ServiceName'),
    (@TemplateId, 13, N'дата утверждения задания для изучения кандидата', N'StudyStages:Утверждение задания'),
    (@TemplateId, 14, N'дата направления установок УП/УР', N'StudyStages:УП/УР'),
    (@TemplateId, 15, N'дата направления кандидата на испытания/результат', N'StudyStages:Испытания'),
    (@TemplateId, 16, N'дата направления на ВВК', N'StudyStages:ВВК.DirectionDate'),
    (@TemplateId, 17, N'дата прохождения и результат ВВК', N'StudyStages:ВВК.Result'),
    (@TemplateId, 18, N'дата и результат ППО', N'StudyStages:ППО.Result'),
    (@TemplateId, 19, N'дата направления проверки ИЦ', N'ServiceChecks:ИЦ'),
    (@TemplateId, 20, N'дата направления проверки ГИАЦ', N'ServiceChecks:ГИАЦ'),
    (@TemplateId, 21, N'дата направления согласования ОРЧ СБ', N'ServiceChecks:ОРЧ СБ'),
    (@TemplateId, 22, N'дата направления материалов в ФСБ', N'ServiceChecks:ФСБ'),
    (@TemplateId, 23, N'дата проверки ГИБДД', N'ServiceChecks:ГИБДД'),
    (@TemplateId, 24, N'дата направления проверки БСТМ', N'ServiceChecks:БСТМ'),
    (@TemplateId, 25, N'дата проверки СООП', N'ServiceChecks:СООП'),
    (@TemplateId, 26, N'дата направления проверки по УВМ', N'ServiceChecks:УВМ'),
    (@TemplateId, 27, N'дата направления проверки диплома', N'ServiceChecks:Проверка диплома'),
    (@TemplateId, 28, N'дата направления материалов на допуск к СГТ', N'ServiceChecks:Допуск к СГТ'),
    (@TemplateId, 29, N'дата и причина отказа', N'Decisions.RefusalReason'),
    (@TemplateId, 30, N'реквизиты приказа о приеме/дата направления в УРЛС', N'Decisions.OrderDetails'),
    (@TemplateId, 31, N'иные сведения', N'ProcessingCards.OtherInfo');
GO
