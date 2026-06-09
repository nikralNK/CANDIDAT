using System;

namespace ExcelCANDIDAT
{
    public class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class VacancyOption
    {
        public int Id { get; set; }
        public string CategoryName { get; set; }
        public string DepartmentName { get; set; }
        public string ServiceName { get; set; }
        public string PositionName { get; set; }
        public string CommandStaffLevel { get; set; }

        public string DisplayName
        {
            get
            {
                return CategoryName + " / " + DepartmentName + " / " + ServiceName + " / " + PositionName;
            }
        }
    }

    public class CandidateCardRow
    {
        public int CardId { get; set; }
        public string FullName { get; set; }
        public DateTime BirthDate { get; set; }
        public string BirthDateText { get { return BirthDate.ToString("dd.MM.yyyy"); } }
        public DateTime StatementDate { get; set; }
        public string StatementDateText { get { return StatementDate.ToString("dd.MM.yyyy"); } }
        public string StudyStage { get; set; }
        public string SourceName { get; set; }
        public string EducationSummary { get; set; }
        public string CategoryName { get; set; }
        public string DepartmentName { get; set; }
        public string ServiceName { get; set; }
        public string PositionName { get; set; }
        public string CommandStaffLevel { get; set; }
        public string OtherInfo { get; set; }
    }

    public class StageRow
    {
        public int StageId { get; set; }
        public string StageTypeName { get; set; }
        public DateTime? DirectionDate { get; set; }
        public string DirectionDateText { get { return DirectionDate.HasValue ? DirectionDate.Value.ToString("dd.MM.yyyy") : ""; } }
        public DateTime? ResultDate { get; set; }
        public string ResultText { get; set; }
    }

    public class CheckRow
    {
        public int CheckId { get; set; }
        public string CheckTypeName { get; set; }
        public DateTime? DirectionDate { get; set; }
        public string DirectionDateText { get { return DirectionDate.HasValue ? DirectionDate.Value.ToString("dd.MM.yyyy") : ""; } }
        public DateTime? ResultDate { get; set; }
        public string ResultText { get; set; }
    }
}
