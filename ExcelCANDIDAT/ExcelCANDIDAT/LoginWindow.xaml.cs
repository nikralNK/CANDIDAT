using System;
using System.Windows;
using System.Windows.Input;

namespace ExcelCANDIDAT
{
    public partial class LoginWindow : Window
    {
        private readonly DatabaseService _database = new DatabaseService();

        public LoginWindow()
        {
            InitializeComponent();
            LoginTextBox.Text = "hr";
            PasswordBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            TryLogin();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryLogin();
            }
        }

        private void TryLogin()
        {
            // Сначала проверяю базу, потому что пользователи хранятся именно там.
            string errorText;
            if (!_database.CanConnect(out errorText))
            {
                MessageBox.Show(
                    "Не удалось подключиться к базе данных.\n\nОшибка: " + errorText,
                    "Вход в систему",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _database.EnsureDefaultUsers();

                var user = _database.AuthenticateUser(LoginTextBox.Text, PasswordBox.Password);
                if (user == null)
                {
                    MessageBox.Show(
                        "Неверный логин или пароль.",
                        "Вход в систему",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var mainWindow = new MainWindow(user);
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось выполнить вход.\n\nОшибка: " + ex.Message,
                    "Вход в систему",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
