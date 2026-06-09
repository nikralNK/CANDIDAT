using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ExcelCANDIDAT
{
    public class DatabaseService
    {
        // Строка подключения берется из App.config, чтобы не писать ее в каждом методе.
        private readonly string _connectionString;

        public DatabaseService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["CandidateDb"].ConnectionString;
        }

        public bool CanConnect(out string errorText)
        {
            // Пробую открыть соединение. Если не получилось, возвращаю текст ошибки для окна сообщения.
            try
            {
                using (var connection = CreateConnection())
                {
                    connection.Open();
                }

                errorText = "";
                return true;
            }
            catch (Exception ex)
            {
                errorText = ex.Message;
                return false;
            }
        }

        public List<CandidateCardRow> GetCards()
        {
            // Этот метод собирает основной список карточек для таблицы в приложении.
            var result = new List<CandidateCardRow>();

            // Здесь соединяю карточку кандидата со справочниками, чтобы вывести понятные названия, а не ID.
            const string sql = @"
SELECT
    pc.CardId,
    c.FullName,
    c.BirthDate,
    pc.StatementDate,
    pc.StudyStage,
    src.SourceName,
    ISNULL(e.EducationLevel, N'') +
        CASE WHEN ISNULL(e.EducationType, N'') = N'' THEN N'' ELSE N', ' + e.EducationType END AS EducationSummary,
    cat.CategoryName,
    d.DepartmentName,
    s.ServiceName,
    p.PositionName,
    p.CommandStaffLevel,
    pc.OtherInfo
FROM dbo.ProcessingCards pc
INNER JOIN dbo.Candidates c ON c.CandidateId = pc.CandidateId
INNER JOIN dbo.InformationSources src ON src.SourceId = pc.SourceId
INNER JOIN dbo.Vacancies v ON v.VacancyId = pc.VacancyId
INNER JOIN dbo.ServiceCategories cat ON cat.CategoryId = v.CategoryId
INNER JOIN dbo.Departments d ON d.DepartmentId = v.DepartmentId
INNER JOIN dbo.Services s ON s.ServiceId = v.ServiceId
INNER JOIN dbo.Positions p ON p.PositionId = v.PositionId
OUTER APPLY (
    SELECT TOP 1 EducationLevel, EducationType
    FROM dbo.Education e
    WHERE e.CandidateId = c.CandidateId
    ORDER BY e.EducationId
) e
ORDER BY pc.StatementDate DESC, c.FullName;";

            using (var connection = CreateConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    // SqlDataReader читает строки по одной, поэтому каждую строку перекладываю в модель.
                    while (reader.Read())
                    {
                        result.Add(new CandidateCardRow
                        {
                            CardId = ReadInt(reader, "CardId"),
                            FullName = ReadString(reader, "FullName"),
                            BirthDate = ReadDate(reader, "BirthDate"),
                            StatementDate = ReadDate(reader, "StatementDate"),
                            StudyStage = ReadString(reader, "StudyStage"),
                            SourceName = ReadString(reader, "SourceName"),
                            EducationSummary = ReadString(reader, "EducationSummary"),
                            CategoryName = ReadString(reader, "CategoryName"),
                            DepartmentName = ReadString(reader, "DepartmentName"),
                            ServiceName = ReadString(reader, "ServiceName"),
                            PositionName = ReadString(reader, "PositionName"),
                            CommandStaffLevel = ReadString(reader, "CommandStaffLevel"),
                            OtherInfo = ReadString(reader, "OtherInfo")
                        });
                    }
                }
            }

            return result;
        }

        public void EnsureDefaultVacancies()
        {
            // Если справочники еще не заполнены, добавляю базовые значения для учебного примера.
            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE CategoryName = N'Юстиция')
    INSERT INTO dbo.ServiceCategories (CategoryName) VALUES (N'Юстиция');
IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE CategoryName = N'Полиция')
    INSERT INTO dbo.ServiceCategories (CategoryName) VALUES (N'Полиция');
IF NOT EXISTS (SELECT 1 FROM dbo.ServiceCategories WHERE CategoryName = N'Внутренняя служба')
    INSERT INTO dbo.ServiceCategories (CategoryName) VALUES (N'Внутренняя служба');

IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentName = N'Следственное управление')
    INSERT INTO dbo.Departments (DepartmentName, DepartmentType, ParentDepartmentId) VALUES (N'Следственное управление', N'Подразделение', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentName = N'Отдел полиции №1')
    INSERT INTO dbo.Departments (DepartmentName, DepartmentType, ParentDepartmentId) VALUES (N'Отдел полиции №1', N'Территориальный орган', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentName = N'Отдел кадров')
    INSERT INTO dbo.Departments (DepartmentName, DepartmentType, ParentDepartmentId) VALUES (N'Отдел кадров', N'Подразделение', NULL);
IF NOT EXISTS (SELECT 1 FROM dbo.Departments WHERE DepartmentName = N'Тыловое обеспечение')
    INSERT INTO dbo.Departments (DepartmentName, DepartmentType, ParentDepartmentId) VALUES (N'Тыловое обеспечение', N'Подразделение', NULL);

IF EXISTS (SELECT 1 FROM dbo.Services WHERE ServiceName = N'Юстиция')
   AND NOT EXISTS (SELECT 1 FROM dbo.Services WHERE ServiceName = N'СО - следственный отдел')
    UPDATE dbo.Services SET ServiceName = N'СО - следственный отдел' WHERE ServiceName = N'Юстиция';
IF NOT EXISTS (SELECT 1 FROM dbo.Services WHERE ServiceName = N'СО - следственный отдел')
    INSERT INTO dbo.Services (ServiceName) VALUES (N'СО - следственный отдел');
IF NOT EXISTS (SELECT 1 FROM dbo.Services WHERE ServiceName = N'Полиция')
    INSERT INTO dbo.Services (ServiceName) VALUES (N'Полиция');
IF NOT EXISTS (SELECT 1 FROM dbo.Services WHERE ServiceName = N'Внутренняя служба')
    INSERT INTO dbo.Services (ServiceName) VALUES (N'Внутренняя служба');

IF NOT EXISTS (SELECT 1 FROM dbo.Positions WHERE PositionName = N'Следователь')
    INSERT INTO dbo.Positions (PositionName, CommandStaffLevel) VALUES (N'Следователь', N'Средний начальствующий состав');
IF NOT EXISTS (SELECT 1 FROM dbo.Positions WHERE PositionName = N'Оперуполномоченный')
    INSERT INTO dbo.Positions (PositionName, CommandStaffLevel) VALUES (N'Оперуполномоченный', N'Средний начальствующий состав');
IF NOT EXISTS (SELECT 1 FROM dbo.Positions WHERE PositionName = N'Участковый уполномоченный полиции')
    INSERT INTO dbo.Positions (PositionName, CommandStaffLevel) VALUES (N'Участковый уполномоченный полиции', N'Средний начальствующий состав');
IF NOT EXISTS (SELECT 1 FROM dbo.Positions WHERE PositionName = N'Специалист по кадрам')
    INSERT INTO dbo.Positions (PositionName, CommandStaffLevel) VALUES (N'Специалист по кадрам', N'Старший начальствующий состав');
IF NOT EXISTS (SELECT 1 FROM dbo.Positions WHERE PositionName = N'Инспектор')
    INSERT INTO dbo.Positions (PositionName, CommandStaffLevel) VALUES (N'Инспектор', N'Средний начальствующий состав');

DECLARE @JusticeCategoryId INT = (SELECT CategoryId FROM dbo.ServiceCategories WHERE CategoryName = N'Юстиция');
DECLARE @PoliceCategoryId INT = (SELECT CategoryId FROM dbo.ServiceCategories WHERE CategoryName = N'Полиция');
DECLARE @InternalCategoryId INT = (SELECT CategoryId FROM dbo.ServiceCategories WHERE CategoryName = N'Внутренняя служба');
DECLARE @JusticeServiceId INT = (SELECT ServiceId FROM dbo.Services WHERE ServiceName = N'СО - следственный отдел');
DECLARE @PoliceServiceId INT = (SELECT ServiceId FROM dbo.Services WHERE ServiceName = N'Полиция');
DECLARE @InternalServiceId INT = (SELECT ServiceId FROM dbo.Services WHERE ServiceName = N'Внутренняя служба');
DECLARE @InvestigationDepartmentId INT = (SELECT DepartmentId FROM dbo.Departments WHERE DepartmentName = N'Следственное управление');
DECLARE @PoliceDepartmentId INT = (SELECT DepartmentId FROM dbo.Departments WHERE DepartmentName = N'Отдел полиции №1');
DECLARE @HrDepartmentId INT = (SELECT DepartmentId FROM dbo.Departments WHERE DepartmentName = N'Отдел кадров');
DECLARE @SupplyDepartmentId INT = (SELECT DepartmentId FROM dbo.Departments WHERE DepartmentName = N'Тыловое обеспечение');
DECLARE @InvestigatorPositionId INT = (SELECT PositionId FROM dbo.Positions WHERE PositionName = N'Следователь');
DECLARE @DetectivePositionId INT = (SELECT PositionId FROM dbo.Positions WHERE PositionName = N'Оперуполномоченный');
DECLARE @DistrictPositionId INT = (SELECT PositionId FROM dbo.Positions WHERE PositionName = N'Участковый уполномоченный полиции');
DECLARE @HrPositionId INT = (SELECT PositionId FROM dbo.Positions WHERE PositionName = N'Специалист по кадрам');
DECLARE @InspectorPositionId INT = (SELECT PositionId FROM dbo.Positions WHERE PositionName = N'Инспектор');

IF NOT EXISTS (SELECT 1 FROM dbo.Vacancies WHERE CategoryId = @JusticeCategoryId AND DepartmentId = @InvestigationDepartmentId AND ServiceId = @JusticeServiceId AND PositionId = @InvestigatorPositionId)
    INSERT INTO dbo.Vacancies (CategoryId, DepartmentId, ServiceId, PositionId) VALUES (@JusticeCategoryId, @InvestigationDepartmentId, @JusticeServiceId, @InvestigatorPositionId);
IF NOT EXISTS (SELECT 1 FROM dbo.Vacancies WHERE CategoryId = @PoliceCategoryId AND DepartmentId = @PoliceDepartmentId AND ServiceId = @PoliceServiceId AND PositionId = @DetectivePositionId)
    INSERT INTO dbo.Vacancies (CategoryId, DepartmentId, ServiceId, PositionId) VALUES (@PoliceCategoryId, @PoliceDepartmentId, @PoliceServiceId, @DetectivePositionId);
IF NOT EXISTS (SELECT 1 FROM dbo.Vacancies WHERE CategoryId = @PoliceCategoryId AND DepartmentId = @PoliceDepartmentId AND ServiceId = @PoliceServiceId AND PositionId = @DistrictPositionId)
    INSERT INTO dbo.Vacancies (CategoryId, DepartmentId, ServiceId, PositionId) VALUES (@PoliceCategoryId, @PoliceDepartmentId, @PoliceServiceId, @DistrictPositionId);
IF NOT EXISTS (SELECT 1 FROM dbo.Vacancies WHERE CategoryId = @InternalCategoryId AND DepartmentId = @HrDepartmentId AND ServiceId = @InternalServiceId AND PositionId = @HrPositionId)
    INSERT INTO dbo.Vacancies (CategoryId, DepartmentId, ServiceId, PositionId) VALUES (@InternalCategoryId, @HrDepartmentId, @InternalServiceId, @HrPositionId);
IF NOT EXISTS (SELECT 1 FROM dbo.Vacancies WHERE CategoryId = @InternalCategoryId AND DepartmentId = @SupplyDepartmentId AND ServiceId = @InternalServiceId AND PositionId = @InspectorPositionId)
    INSERT INTO dbo.Vacancies (CategoryId, DepartmentId, ServiceId, PositionId) VALUES (@InternalCategoryId, @SupplyDepartmentId, @InternalServiceId, @InspectorPositionId);";

            // Добавляю базовые варианты, чтобы в выпадающем списке сразу было что выбрать.
            ExecuteNonQuery(sql);
        }

        public List<LookupItem> GetSources()
        {
            // Источник сведений: рекомендация, объявление и другие варианты.
            return GetLookupItems("SELECT SourceId AS Id, SourceName AS Name FROM dbo.InformationSources ORDER BY SourceName");
        }

        public List<LookupItem> GetCategories()
        {
            // Категория службы нужна, чтобы потом отфильтровать подходящие должности.
            return GetLookupItems("SELECT CategoryId AS Id, CategoryName AS Name FROM dbo.ServiceCategories ORDER BY CategoryName");
        }

        public List<LookupItem> GetStageTypes()
        {
            // Типы этапов изучения кандидата выводятся в выпадающем списке.
            return GetLookupItems("SELECT StageTypeId AS Id, StageTypeName AS Name FROM dbo.StudyStageTypes ORDER BY StageTypeId");
        }

        public List<LookupItem> GetCheckTypes()
        {
            // Типы служебных проверок тоже берутся из справочника.
            return GetLookupItems("SELECT CheckTypeId AS Id, CheckTypeName AS Name FROM dbo.CheckTypes ORDER BY CheckTypeId");
        }

        public List<VacancyOption> GetVacancies(int? categoryId)
        {
            // Возвращаю вакансии по выбранной категории службы.
            var result = new List<VacancyOption>();

            // Вакансия состоит из категории, подразделения, службы и должности.
            const string sql = @"
SELECT
    v.VacancyId,
    cat.CategoryName,
    d.DepartmentName,
    s.ServiceName,
    p.PositionName,
    p.CommandStaffLevel
FROM dbo.Vacancies v
INNER JOIN dbo.ServiceCategories cat ON cat.CategoryId = v.CategoryId
INNER JOIN dbo.Departments d ON d.DepartmentId = v.DepartmentId
INNER JOIN dbo.Services s ON s.ServiceId = v.ServiceId
INNER JOIN dbo.Positions p ON p.PositionId = v.PositionId
WHERE (@CategoryId IS NULL OR v.CategoryId = @CategoryId)
ORDER BY cat.CategoryName, d.DepartmentName, s.ServiceName, p.PositionName;";

            using (var connection = CreateConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CategoryId", SqlDbType.Int).Value = (object)categoryId ?? DBNull.Value;
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new VacancyOption
                        {
                            Id = ReadInt(reader, "VacancyId"),
                            CategoryName = ReadString(reader, "CategoryName"),
                            DepartmentName = ReadString(reader, "DepartmentName"),
                            ServiceName = ReadString(reader, "ServiceName"),
                            PositionName = ReadString(reader, "PositionName"),
                            CommandStaffLevel = ReadString(reader, "CommandStaffLevel")
                        });
                    }
                }
            }

            return result;
        }

        public int CreateProcessingCard(
            string fullName,
            DateTime birthDate,
            string phone,
            string email,
            string educationLevel,
            string educationType,
            string institution,
            int sourceId,
            int vacancyId,
            DateTime statementDate,
            string otherInfo)
        {
            // Карточка создается в транзакции: если одна вставка не пройдет, остальные тоже откатятся.
            using (var connection = CreateConnection())
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Сначала сохраняю самого кандидата.
                        var candidateId = ExecuteScalarInt(connection, transaction, @"
INSERT INTO dbo.Candidates (FullName, BirthDate, Phone, Email)
VALUES (@FullName, @BirthDate, @Phone, @Email);
SELECT SCOPE_IDENTITY();",
                            new SqlParameter("@FullName", fullName),
                            new SqlParameter("@BirthDate", birthDate),
                            new SqlParameter("@Phone", NullIfEmpty(phone)),
                            new SqlParameter("@Email", NullIfEmpty(email)));

                        // Потом сохраняю сведения об образовании кандидата.
                        ExecuteNonQuery(connection, transaction, @"
INSERT INTO dbo.Education (CandidateId, EducationLevel, EducationType, Institution, DocumentInfo)
VALUES (@CandidateId, @EducationLevel, @EducationType, @Institution, NULL);",
                            new SqlParameter("@CandidateId", candidateId),
                            new SqlParameter("@EducationLevel", educationLevel),
                            new SqlParameter("@EducationType", NullIfEmpty(educationType)),
                            new SqlParameter("@Institution", NullIfEmpty(institution)));

                        // Тут создается карточка кандидата, с ней кадровик потом работает дальше.
                        var cardId = ExecuteScalarInt(connection, transaction, @"
INSERT INTO dbo.ProcessingCards
    (CandidateId, SourceId, VacancyId, HrUserId, StatementDate, StudyStage, OtherInfo)
VALUES
    (@CandidateId, @SourceId, @VacancyId, @HrUserId, @StatementDate, N'На оформлении', @OtherInfo);
SELECT SCOPE_IDENTITY();",
                            new SqlParameter("@CandidateId", candidateId),
                            new SqlParameter("@SourceId", sourceId),
                            new SqlParameter("@VacancyId", vacancyId),
                            new SqlParameter("@HrUserId", EnsureDefaultHrUser(connection, transaction)),
                            new SqlParameter("@StatementDate", statementDate),
                            new SqlParameter("@OtherInfo", NullIfEmpty(otherInfo)));

                        transaction.Commit();
                        // Возвращаю ID созданной карточки, если он понадобится дальше.
                        return cardId;
                    }
                    catch
                    {
                        // Если при сохранении произошла ошибка, отменяю все изменения этой операции.
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public List<StageRow> GetStages(int cardId)
        {
            // Получаю этапы изучения только для выбранной карточки.
            var result = new List<StageRow>();

            const string sql = @"
SELECT ss.StageId, st.StageTypeName, ss.DirectionDate, ss.ResultDate, ss.ResultText
FROM dbo.StudyStages ss
INNER JOIN dbo.StudyStageTypes st ON st.StageTypeId = ss.StageTypeId
WHERE ss.CardId = @CardId
ORDER BY st.StageTypeId;";

            using (var connection = CreateConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CardId", SqlDbType.Int).Value = cardId;
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new StageRow
                        {
                            StageId = ReadInt(reader, "StageId"),
                            StageTypeName = ReadString(reader, "StageTypeName"),
                            DirectionDate = ReadNullableDate(reader, "DirectionDate"),
                            ResultDate = ReadNullableDate(reader, "ResultDate"),
                            ResultText = ReadString(reader, "ResultText")
                        });
                    }
                }
            }

            return result;
        }

        public List<CheckRow> GetChecks(int cardId)
        {
            // Получаю служебные проверки только для выбранной карточки.
            var result = new List<CheckRow>();

            const string sql = @"
SELECT sc.CheckId, ct.CheckTypeName, sc.DirectionDate, sc.ResultDate, sc.ResultText
FROM dbo.ServiceChecks sc
INNER JOIN dbo.CheckTypes ct ON ct.CheckTypeId = sc.CheckTypeId
WHERE sc.CardId = @CardId
ORDER BY ct.CheckTypeId;";

            using (var connection = CreateConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@CardId", SqlDbType.Int).Value = cardId;
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new CheckRow
                        {
                            CheckId = ReadInt(reader, "CheckId"),
                            CheckTypeName = ReadString(reader, "CheckTypeName"),
                            DirectionDate = ReadNullableDate(reader, "DirectionDate"),
                            ResultDate = ReadNullableDate(reader, "ResultDate"),
                            ResultText = ReadString(reader, "ResultText")
                        });
                    }
                }
            }

            return result;
        }

        public void SaveStage(int cardId, int stageTypeId, DateTime? directionDate, DateTime? resultDate, string resultText)
        {
            // Если такой этап уже есть, обновляю его. Если нет, создаю новую запись.
            const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.StudyStages WHERE CardId = @CardId AND StageTypeId = @StageTypeId)
BEGIN
    UPDATE dbo.StudyStages
    SET DirectionDate = @DirectionDate, ResultDate = @ResultDate, ResultText = @ResultText
    WHERE CardId = @CardId AND StageTypeId = @StageTypeId;
END
ELSE
BEGIN
    INSERT INTO dbo.StudyStages (CardId, StageTypeId, DirectionDate, ResultDate, ResultText)
    VALUES (@CardId, @StageTypeId, @DirectionDate, @ResultDate, @ResultText);
END";

            ExecuteNonQuery(sql,
                new SqlParameter("@CardId", cardId),
                new SqlParameter("@StageTypeId", stageTypeId),
                new SqlParameter("@DirectionDate", NullDate(directionDate)),
                new SqlParameter("@ResultDate", NullDate(resultDate)),
                new SqlParameter("@ResultText", NullIfEmpty(resultText)));
        }

        public void SaveCheck(int cardId, int checkTypeId, DateTime? directionDate, DateTime? resultDate, string resultText)
        {
            // Проверки сохраняются по такому же принципу: обновить существующую или добавить новую.
            const string sql = @"
IF EXISTS (SELECT 1 FROM dbo.ServiceChecks WHERE CardId = @CardId AND CheckTypeId = @CheckTypeId)
BEGIN
    UPDATE dbo.ServiceChecks
    SET DirectionDate = @DirectionDate, ResultDate = @ResultDate, ResultText = @ResultText
    WHERE CardId = @CardId AND CheckTypeId = @CheckTypeId;
END
ELSE
BEGIN
    INSERT INTO dbo.ServiceChecks (CardId, CheckTypeId, DirectionDate, ResultDate, ResultText)
    VALUES (@CardId, @CheckTypeId, @DirectionDate, @ResultDate, @ResultText);
END";

            ExecuteNonQuery(sql,
                new SqlParameter("@CardId", cardId),
                new SqlParameter("@CheckTypeId", checkTypeId),
                new SqlParameter("@DirectionDate", NullDate(directionDate)),
                new SqlParameter("@ResultDate", NullDate(resultDate)),
                new SqlParameter("@ResultText", NullIfEmpty(resultText)));
        }

        public void SaveDecision(int cardId, string decisionType, DateTime? decisionDate, string refusalReason, string orderDetails)
        {
            // Решение хранится отдельно, но еще меняет стадию самой карточки.
            const string decisionSql = @"
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
END";

            // Перевожу выбранное решение в статус, который показывается в таблице карточек.
            var stage = decisionType == "Принят" ? "Принят на службу" :
                decisionType == "Отказ" ? "Отказ" : "На оформлении";

            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    ExecuteNonQuery(connection, transaction, decisionSql,
                        new SqlParameter("@CardId", cardId),
                        new SqlParameter("@DecisionType", decisionType),
                        new SqlParameter("@DecisionDate", NullDate(decisionDate)),
                        new SqlParameter("@RefusalReason", NullIfEmpty(refusalReason)),
                        new SqlParameter("@OrderDetails", NullIfEmpty(orderDetails)));

                    ExecuteNonQuery(connection, transaction,
                        "UPDATE dbo.ProcessingCards SET StudyStage = @StudyStage WHERE CardId = @CardId;",
                        new SqlParameter("@StudyStage", stage),
                        new SqlParameter("@CardId", cardId));

                    transaction.Commit();
                }
            }
        }

        private List<LookupItem> GetLookupItems(string sql)
        {
            // Общий метод для простых справочников, где есть только ID и название.
            var result = new List<LookupItem>();

            using (var connection = CreateConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new LookupItem
                        {
                            Id = ReadInt(reader, "Id"),
                            Name = ReadString(reader, "Name")
                        });
                    }
                }
            }

            return result;
        }

        private int EnsureDefaultHrUser(SqlConnection connection, SqlTransaction transaction)
        {
            // Для учебного проекта создаю кадровика автоматически, если его еще нет в базе.
            const string findSql = "SELECT TOP 1 UserId FROM dbo.Users WHERE RoleName = N'Кадровик' ORDER BY UserId;";
            using (var findCommand = new SqlCommand(findSql, connection, transaction))
            {
                var value = findCommand.ExecuteScalar();
                if (value != null && value != DBNull.Value)
                {
                    return Convert.ToInt32(value);
                }
            }

            return ExecuteScalarInt(connection, transaction, @"
INSERT INTO dbo.Users (FullName, Login, PasswordHash, RoleName)
VALUES (N'Кадровик', N'hr', N'not_used_in_demo', N'Кадровик');
SELECT SCOPE_IDENTITY();");
        }

        private void ExecuteNonQuery(string sql, params SqlParameter[] parameters)
        {
            // Выполняю SQL-команду, которая ничего не возвращает: INSERT, UPDATE и похожие запросы.
            using (var connection = CreateConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddRange(parameters);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        private static void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            // Такой же метод, но для команд внутри транзакции.
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                command.ExecuteNonQuery();
            }
        }

        private static int ExecuteScalarInt(SqlConnection connection, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
        {
            // Использую для INSERT ... SELECT SCOPE_IDENTITY(), чтобы получить ID новой записи.
            using (var command = new SqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddRange(parameters);
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private SqlConnection CreateConnection()
        {
            // Создаю новое подключение каждый раз, а using потом сам его закрывает.
            return new SqlConnection(_connectionString);
        }

        private static object NullIfEmpty(string value)
        {
            // Пустые строки лучше сохранять как NULL, чтобы в базе не было лишних пробелов.
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static object NullDate(DateTime? value)
        {
            // Если дата не выбрана, в базу тоже отправляется NULL.
            return value.HasValue ? (object)value.Value : DBNull.Value;
        }

        private static int ReadInt(SqlDataReader reader, string columnName)
        {
            // Маленькие методы чтения нужны, чтобы не повторять Convert в каждом месте.
            return Convert.ToInt32(reader[columnName]);
        }

        private static string ReadString(SqlDataReader reader, string columnName)
        {
            var value = reader[columnName];
            return value == DBNull.Value ? "" : Convert.ToString(value);
        }

        private static DateTime ReadDate(SqlDataReader reader, string columnName)
        {
            return Convert.ToDateTime(reader[columnName]);
        }

        private static DateTime? ReadNullableDate(SqlDataReader reader, string columnName)
        {
            var value = reader[columnName];
            return value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value);
        }
    }
}
