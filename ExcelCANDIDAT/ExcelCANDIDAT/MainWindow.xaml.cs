using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ExcelCANDIDAT
{
    public partial class MainWindow : Window
    {
        // Сервис нужен, чтобы вся работа с базой была в одном месте.
        private readonly DatabaseService _database = new DatabaseService();

        // Здесь храню карточки, которые показываются в таблице слева и в отчете.
        private List<CandidateCardRow> _cards = new List<CandidateCardRow>();

        // Это список вакансий для выбранной категории, из него потом выбирается должность и служба.
        private List<VacancyOption> _categoryVacancies = new List<VacancyOption>();

        public MainWindow()
        {
            InitializeComponent();
            PrepareDefaultValues();
            LoadFromDatabase();
        }

        private CandidateCardRow SelectedCard
        {
            // Так удобнее получать выбранную карточку, чтобы не писать одно и то же в разных методах.
            get { return CardsGrid.SelectedItem as CandidateCardRow; }
        }

        private void PrepareDefaultValues()
        {
            // Сразу выставляю начальные значения, чтобы форма не была пустой при запуске.
            var maxBirthDate = GetMaxCandidateBirthDate();
            BirthDatePicker.DisplayDateEnd = maxBirthDate;
            BirthDatePicker.BlackoutDates.Add(new CalendarDateRange(maxBirthDate.AddDays(1), DateTime.MaxValue));
            BirthDatePicker.SelectedDate = new DateTime(2000, 1, 1);
            StatementDatePicker.SelectedDate = DateTime.Today;
            StageDirectionDatePicker.SelectedDate = DateTime.Today;
            CheckDirectionDatePicker.SelectedDate = DateTime.Today;
            DecisionDatePicker.SelectedDate = DateTime.Today;
            DecisionTypeComboBox.SelectedIndex = 0;

            ReportMonthComboBox.ItemsSource = Enumerable.Range(1, 12).ToList();
            ReportMonthComboBox.SelectedItem = DateTime.Today.Month;
            ReportYearTextBox.Text = DateTime.Today.Year.ToString();
        }

        private DateTime GetMaxCandidateBirthDate()
        {
            // Ограничиваю дату, потому что кандидату должно быть минимум 18 лет.
            return DateTime.Today.AddYears(-18);
        }

        private void LoadFromDatabase()
        {
            // Сначала проверяю подключение, потому что без базы большая часть формы работать не сможет.
            string errorText;
            if (!_database.CanConnect(out errorText))
            {
                MessageBox.Show(
                    "Не удалось подключиться к базе данных.\n\n" +
                    "Проверь SQL Server и выполни скрипт create_candidate_accounting_db_sql_server_2022.sql.\n\n" +
                    "Ошибка: " + errorText,
                    "Подключение к БД",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _database.EnsureDefaultVacancies();

            // Загружаю справочники в выпадающие списки.
            SourceComboBox.ItemsSource = _database.GetSources();
            CategoryComboBox.ItemsSource = _database.GetCategories();
            StageTypeComboBox.ItemsSource = _database.GetStageTypes();
            CheckTypeComboBox.ItemsSource = _database.GetCheckTypes();

            // Выбираю первые значения, чтобы кадровику не приходилось начинать с пустых списков.
            if (SourceComboBox.Items.Count > 0) SourceComboBox.SelectedIndex = 0;
            if (CategoryComboBox.Items.Count > 0) CategoryComboBox.SelectedIndex = 0;
            if (StageTypeComboBox.Items.Count > 0) StageTypeComboBox.SelectedIndex = 0;
            if (CheckTypeComboBox.Items.Count > 0) CheckTypeComboBox.SelectedIndex = 0;

            RefreshCards();
        }

        private void RefreshCards()
        {
            // Обновляю список карточек после сохранения или ручного нажатия кнопки "Обновить".
            _cards = _database.GetCards();
            CardsGrid.ItemsSource = _cards;
            ReportGrid.ItemsSource = _cards;

            if (_cards.Count > 0)
            {
                CardsGrid.SelectedIndex = 0;
            }
            else
            {
                RefreshSelectedCardDetails();
            }
        }

        private void RefreshSelectedCardDetails()
        {
            // Когда выбираем карточку, справа показываются ее этапы и проверки.
            var card = SelectedCard;

            if (card == null)
            {
                SelectedCardTextBlock.Text = "Карточка не выбрана";
                StagesGrid.ItemsSource = null;
                ChecksGrid.ItemsSource = null;
                return;
            }

            SelectedCardTextBlock.Text = card.FullName + " | " + card.PositionName + " | " + card.StudyStage;
            StagesGrid.ItemsSource = _database.GetStages(card.CardId);
            ChecksGrid.ItemsSource = _database.GetChecks(card.CardId);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshCards();
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Категория влияет на список доступных должностей.
            var category = CategoryComboBox.SelectedItem as LookupItem;
            if (category == null)
            {
                _categoryVacancies.Clear();
                VacancyComboBox.ItemsSource = null;
                ServiceNameComboBox.ItemsSource = null;
                return;
            }

            // После категории показываю только те должности, которые к ней подходят.
            _categoryVacancies = _database.GetVacancies(category.Id);
            VacancyComboBox.ItemsSource = _categoryVacancies
                .GroupBy(x => x.PositionName)
                .Select(x => x.First())
                .ToList();

            if (VacancyComboBox.Items.Count > 0)
            {
                VacancyComboBox.SelectedIndex = 0;
            }
            else
            {
                ServiceNameComboBox.ItemsSource = null;
            }
        }

        private void VacancyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // После выбора должности оставляю только те службы, где такая должность есть.
            var vacancy = VacancyComboBox.SelectedItem as VacancyOption;
            if (vacancy == null)
            {
                ServiceNameComboBox.ItemsSource = null;
                return;
            }

            ServiceNameComboBox.ItemsSource = _categoryVacancies
                .Where(x => x.PositionName == vacancy.PositionName)
                .GroupBy(x => x.ServiceName)
                .Select(x => x.First())
                .ToList();

            if (ServiceNameComboBox.Items.Count > 0)
            {
                ServiceNameComboBox.SelectedIndex = 0;
            }
        }

        private void CardsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshSelectedCardDetails();
        }

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null || comboBox.IsDropDownOpen)
            {
                return;
            }

            // Отключаю случайную смену пункта колесиком мыши.
            e.Handled = true;

            var parent = FindParent<UIElement>(comboBox);
            if (parent != null)
            {
                parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = MouseWheelEvent,
                    Source = comboBox
                });
            }
        }

        private void CreateCardButton_Click(object sender, RoutedEventArgs e)
        {
            // Перед сохранением проверяю форму, чтобы в базу не ушли пустые или неправильные данные.
            if (!ValidateCardForm())
            {
                return;
            }

            var source = (LookupItem)SourceComboBox.SelectedItem;
            var vacancy = GetSelectedVacancy();

            // Кадровик вносит данные сам, поэтому сразу сохраняем карточку оформления.
            _database.CreateProcessingCard(
                BuildCandidateFullName(),
                BirthDatePicker.SelectedDate.Value,
                PhoneTextBox.Text.Trim(),
                EmailTextBox.Text.Trim(),
                EducationLevelTextBox.Text.Trim(),
                EducationTypeTextBox.Text.Trim(),
                InstitutionTextBox.Text.Trim(),
                source.Id,
                vacancy.Id,
                StatementDatePicker.SelectedDate.Value,
                OtherInfoTextBox.Text.Trim());

            ClearCardForm();
            RefreshCards();
            MessageBox.Show("Карточка кандидата создана.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool ValidateCardForm()
        {
            // Здесь собраны проверки для карточки кандидата. Если что-то не заполнено, показываю предупреждение.
            if (string.IsNullOrWhiteSpace(LastNameTextBox.Text))
            {
                ShowWarning("Введите фамилию кандидата.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(FirstNameTextBox.Text))
            {
                ShowWarning("Введите имя кандидата.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(MiddleNameTextBox.Text))
            {
                ShowWarning("Введите отчество кандидата.");
                return false;
            }

            if (BirthDatePicker.SelectedDate == null)
            {
                ShowWarning("Выберите дату рождения.");
                return false;
            }

            if (BirthDatePicker.SelectedDate.Value.Date > GetMaxCandidateBirthDate())
            {
                ShowWarning("Кандидату должно быть не меньше 18 лет.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(EducationLevelTextBox.Text))
            {
                ShowWarning("Введите уровень образования.");
                return false;
            }

            if (SourceComboBox.SelectedItem == null)
            {
                ShowWarning("Выберите источник сведений.");
                return false;
            }

            if (VacancyComboBox.SelectedItem == null)
            {
                ShowWarning("Выберите должность.");
                return false;
            }

            if (ServiceNameComboBox.SelectedItem == null)
            {
                ShowWarning("Выберите наименование службы.");
                return false;
            }

            if (GetSelectedVacancy() == null)
            {
                ShowWarning("Не найдена связка должности и службы.");
                return false;
            }

            if (StatementDatePicker.SelectedDate == null)
            {
                ShowWarning("Выберите дату заявления.");
                return false;
            }

            return true;
        }

        private VacancyOption GetSelectedVacancy()
        {
            // В интерфейсе должность и служба выбираются отдельно, а в базе хранится одна связка VacancyId.
            var position = VacancyComboBox.SelectedItem as VacancyOption;
            var service = ServiceNameComboBox.SelectedItem as VacancyOption;

            if (position == null || service == null)
            {
                return null;
            }

            return _categoryVacancies.FirstOrDefault(x =>
                x.PositionName == position.PositionName &&
                x.ServiceName == service.ServiceName);
        }

        private string BuildCandidateFullName()
        {
            // В форме ФИО разделено, а в базу и отчет отправляю одной строкой.
            return LastNameTextBox.Text.Trim() + " " +
                FirstNameTextBox.Text.Trim() + " " +
                MiddleNameTextBox.Text.Trim();
        }

        private void ClearCardForm()
        {
            // Очищаю форму после сохранения, чтобы можно было сразу заносить следующего кандидата.
            LastNameTextBox.Clear();
            FirstNameTextBox.Clear();
            MiddleNameTextBox.Clear();
            PhoneTextBox.Clear();
            EmailTextBox.Clear();
            EducationLevelTextBox.Clear();
            EducationTypeTextBox.Clear();
            InstitutionTextBox.Clear();
            OtherInfoTextBox.Clear();
            BirthDatePicker.SelectedDate = new DateTime(2000, 1, 1);
            StatementDatePicker.SelectedDate = DateTime.Today;
        }

        private void SaveStageButton_Click(object sender, RoutedEventArgs e)
        {
            // Этап изучения сохраняется только для выбранной карточки кандидата.
            var card = SelectedCard;
            var stageType = StageTypeComboBox.SelectedItem as LookupItem;

            if (card == null)
            {
                ShowWarning("Выберите карточку кандидата.");
                return;
            }

            if (stageType == null)
            {
                ShowWarning("Выберите тип этапа.");
                return;
            }

            _database.SaveStage(card.CardId, stageType.Id, StageDirectionDatePicker.SelectedDate, StageResultDatePicker.SelectedDate, StageResultTextBox.Text);
            StageResultTextBox.Clear();
            RefreshSelectedCardDetails();
        }

        private void SaveCheckButton_Click(object sender, RoutedEventArgs e)
        {
            // Служебная проверка тоже привязывается к выбранной карточке.
            var card = SelectedCard;
            var checkType = CheckTypeComboBox.SelectedItem as LookupItem;

            if (card == null)
            {
                ShowWarning("Выберите карточку кандидата.");
                return;
            }

            if (checkType == null)
            {
                ShowWarning("Выберите тип проверки.");
                return;
            }

            _database.SaveCheck(card.CardId, checkType.Id, CheckDirectionDatePicker.SelectedDate, CheckResultDatePicker.SelectedDate, CheckResultTextBox.Text);
            CheckResultTextBox.Clear();
            RefreshSelectedCardDetails();
        }

        private void SaveDecisionButton_Click(object sender, RoutedEventArgs e)
        {
            // Итоговое решение меняет общий статус карточки кандидата.
            var card = SelectedCard;
            if (card == null)
            {
                ShowWarning("Выберите карточку кандидата.");
                return;
            }

            var item = DecisionTypeComboBox.SelectedItem as ComboBoxItem;
            var decisionType = item == null ? "На оформлении" : item.Content.ToString();

            if (decisionType == "Отказ" && string.IsNullOrWhiteSpace(RefusalReasonTextBox.Text))
            {
                ShowWarning("Для отказа нужно указать причину.");
                return;
            }

            if (decisionType == "Принят" && string.IsNullOrWhiteSpace(OrderDetailsTextBox.Text))
            {
                ShowWarning("Для принятого кандидата нужно указать реквизиты приказа.");
                return;
            }

            _database.SaveDecision(card.CardId, decisionType, DecisionDatePicker.SelectedDate, RefusalReasonTextBox.Text, OrderDetailsTextBox.Text);
            RefreshCards();
        }

        private void ExportReportButton_Click(object sender, RoutedEventArgs e)
        {
            // Для отчета берем только карточки за выбранный месяц и год.
            int month;
            int year;

            if (ReportMonthComboBox.SelectedItem == null ||
                !int.TryParse(ReportMonthComboBox.SelectedItem.ToString(), out month) ||
                !int.TryParse(ReportYearTextBox.Text, out year))
            {
                ShowWarning("Укажите месяц и год отчета.");
                return;
            }

            var cards = _cards.Where(x => x.StatementDate.Month == month && x.StatementDate.Year == year).ToList();

            var dialog = new SaveFileDialog
            {
                Title = "Сохранить Excel-отчет",
                Filter = "Excel XML (*.xls)|*.xls",
                FileName = "Отчет_по_кандидатам_" + month.ToString("00") + "_" + year + ".xls"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, BuildExcelXml(cards), Encoding.UTF8);
            MessageBox.Show("Отчет создан. Его можно открыть в Excel.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string BuildExcelXml(List<CandidateCardRow> cards)
        {
            // Здесь отчет собирается автоматически, чтобы не заполнять Excel вручную.
            var builder = new StringBuilder();
            builder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            builder.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            builder.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            builder.AppendLine("<Worksheet ss:Name=\"Отчет\"><Table>");

            AppendRow(builder, "№ п/п", "Дата заявления", "Стадия", "ФИО", "Дата рождения", "Образование",
                "Источник", "Подразделение", "Начальствующий состав", "Категория", "Должность", "Служба", "Иные сведения");

            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                AppendRow(builder,
                    (i + 1).ToString(),
                    card.StatementDateText,
                    card.StudyStage,
                    card.FullName,
                    card.BirthDateText,
                    card.EducationSummary,
                    card.SourceName,
                    card.DepartmentName,
                    card.CommandStaffLevel,
                    card.CategoryName,
                    card.PositionName,
                    card.ServiceName,
                    card.OtherInfo);
            }

            builder.AppendLine("</Table></Worksheet></Workbook>");
            return builder.ToString();
        }

        private static void AppendRow(StringBuilder builder, params string[] values)
        {
            // Каждая строка Excel-отчета собирается из обычных ячеек XML.
            builder.AppendLine("<Row>");
            foreach (var value in values)
            {
                builder.Append("<Cell><Data ss:Type=\"String\">");
                builder.Append(SecurityElement.Escape(value ?? ""));
                builder.AppendLine("</Data></Cell>");
            }
            builder.AppendLine("</Row>");
        }

        private static void ShowWarning(string text)
        {
            // Общий метод для предупреждений, чтобы все сообщения выглядели одинаково.
            MessageBox.Show(text, "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            // Ищу родительский элемент, чтобы прокрутка колесиком продолжала работать у всей формы.
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
            {
                return null;
            }

            var parent = parentObject as T;
            return parent ?? FindParent<T>(parentObject);
        }
    }
}
