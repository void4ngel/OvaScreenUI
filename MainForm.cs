using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace OvaScreenUI
{
    public partial class MainForm : Form
    {
        private readonly HttpClient httpClient = new HttpClient();
        private readonly string baseUrl = "http://localhost:8000";

        // Кнопки
        private Button btnLoadDicom;
        private Button btnPatientHistory;
        private Button btnScansHistory;

        // Контролы для изображения
        private Panel panelImageContainer;
        private PictureBox pictureBoxResult;

        // Ползунок прозрачности (вертикальный)
        private TrackBar trackBarOpacity;
        private Label labelOpacity;

        // Текстбокс для информации
        private TextBox textBoxInfo;

        private Label labelStatus;
        private OpenFileDialog openFileDialog;

        private Bitmap originalImage;
        private Bitmap maskImage;

        public MainForm()
        {
            InitializeComponent();
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = "OvaScreenUI - AI Сегментация УЗИ";
            this.Size = new Size(820, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // ========== ВЕРХНЯЯ ПАНЕЛЬ С КНОПКАМИ ==========

            // Кнопка "Загрузить DICOM" (СЛЕВА)
            btnLoadDicom = new Button
            {
                Text = "Загрузить DICOM",
                Location = new Point(20, 20),
                Size = new Size(150, 35),
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat
            };
            btnLoadDicom.Click += BtnLoadDicom_Click;

            // Кнопка "История пациента" (СПРАВА)
            btnPatientHistory = new Button
            {
                Text = "История пациента",
                Location = new Point(this.ClientSize.Width - 170, 20),
                Size = new Size(140, 35),
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BackColor = Color.LightSteelBlue,
                FlatStyle = FlatStyle.Flat
            };
            btnPatientHistory.Click += BtnPatientHistory_Click;

            // Кнопка "История снимков" (СПРАВА, РЯДОМ С ПРЕДЫДУЩЕЙ)
            btnScansHistory = new Button
            {
                Text = "История снимков",
                Location = new Point(this.ClientSize.Width - 320, 20),
                Size = new Size(140, 35),
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                BackColor = Color.LightSteelBlue,
                FlatStyle = FlatStyle.Flat
            };
            btnScansHistory.Click += BtnScansHistory_Click;

            // ========== КОНТЕЙНЕР ДЛЯ ИЗОБРАЖЕНИЯ ==========
            panelImageContainer = new Panel
            {
                Location = new Point(20, 70),
                Size = new Size(700, 500),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };

            pictureBoxResult = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };
            panelImageContainer.Controls.Add(pictureBoxResult);

            // ========== ВЕРТИКАЛЬНЫЙ ПОЛЗУНОК СПРАВА ОТ СНИМКА ==========
            trackBarOpacity = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                Location = new Point(740, 70),
                Size = new Size(45, 500),
                Enabled = false,
                TickFrequency = 10,
                Orientation = Orientation.Vertical
            };
            trackBarOpacity.Scroll += TrackBarOpacity_Scroll;

            labelOpacity = new Label
            {
                Text = "50%",
                Location = new Point(750, 580),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // ========== ТЕКСТБОКС ВНИЗУ ==========
            textBoxInfo = new TextBox
            {
                Location = new Point(20, 590),
                Size = new Size(765, 100),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.WhiteSmoke,
                Text = "Информация будет отображаться здесь..."
            };

            // ========== СТАТУС ==========
            labelStatus = new Label
            {
                Text = "Готов",
                Location = new Point(20, 700),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Italic)
            };

            // ========== DIALOG ==========
            openFileDialog = new OpenFileDialog { Filter = "DICOM files|*.dcm" };

            // ========== ДОБАВЛЯЕМ ВСЕ КОНТРОЛЫ НА ФОРМУ ==========
            this.Controls.Add(btnLoadDicom);
            this.Controls.Add(btnPatientHistory);
            this.Controls.Add(btnScansHistory);
            this.Controls.Add(panelImageContainer);
            this.Controls.Add(trackBarOpacity);
            this.Controls.Add(labelOpacity);
            this.Controls.Add(textBoxInfo);
            this.Controls.Add(labelStatus);

            this.Load += MainForm_Load;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            // При изменении размера окна корректируем положение кнопок справа
            btnPatientHistory.Location = new Point(this.ClientSize.Width - 170, 20);
            btnScansHistory.Location = new Point(this.ClientSize.Width - 320, 20);
        }


        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                var response = await httpClient.GetAsync($"{baseUrl}/");
                if (response.IsSuccessStatusCode)
                    labelStatus.Text = "Сервер доступен";
                else
                    labelStatus.Text = "Сервер не отвечает";
            }
            catch
            {
                labelStatus.Text = "Сервер не запущен";
            }
        }

        private async void BtnLoadDicom_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() != DialogResult.OK) return;

            string dicomPath = openFileDialog.FileName;
            labelStatus.Text = "Отправка на сервер...";
            btnLoadDicom.Enabled = false;

            try
            {
                using (var formData = new MultipartFormDataContent())
                {
                    byte[] fileBytes = File.ReadAllBytes(dicomPath);
                    var content = new ByteArrayContent(fileBytes);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    formData.Add(content, "file", Path.GetFileName(dicomPath));

                    HttpResponseMessage response = await httpClient.PostAsync($"{baseUrl}/upload-dicom", formData);
                    string jsonString = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"Ошибка сервера: {jsonString}");

                    var json = JObject.Parse(jsonString);
                    string originalUrl = json["original_image_url"].ToString();
                    string maskUrl = json["ai_mask_url"].ToString();
                    double confidence = (double)json["confidence"];

                    // Скачиваем изображения
                    byte[] originalData = await httpClient.GetByteArrayAsync($"{baseUrl}{originalUrl}");
                    byte[] maskData = await httpClient.GetByteArrayAsync($"{baseUrl}{maskUrl}");

                    using (var msOrig = new MemoryStream(originalData))
                    using (var msMask = new MemoryStream(maskData))
                    {
                        originalImage = new Bitmap(Image.FromStream(msOrig));
                        maskImage = new Bitmap(Image.FromStream(msMask));
                    }

                    // Показываем совмещённое изображение
                    UpdateOverlay();

                    trackBarOpacity.Enabled = true;
                    labelStatus.Text = $"Готово (уверенность: {confidence:P3})";

                    // Добавляем информацию в текстбокс
                    AddInfoMessage($"[{DateTime.Now:HH:mm:ss}] Загружен снимок: {Path.GetFileName(dicomPath)}");
                    AddInfoMessage($"[{DateTime.Now:HH:mm:ss}] Уверенность модели: {confidence:P3}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n\nУбедитесь, что сервер запущен на {baseUrl}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                labelStatus.Text = "Ошибка";
                AddInfoMessage($"[{DateTime.Now:HH:mm:ss}] ОШИБКА: {ex.Message}");
            }
            finally
            {
                btnLoadDicom.Enabled = true;
            }
        }

        private void BtnPatientHistory_Click(object sender, EventArgs e)
        {
            AddInfoMessage($"[{DateTime.Now:HH:mm:ss}] Запрос истории пациента...");
            AddInfoMessage("Функция в разработке. Здесь будет отображаться история пациента.");
        }

        private void BtnScansHistory_Click(object sender, EventArgs e)
        {
            AddInfoMessage($"[{DateTime.Now:HH:mm:ss}] Запрос истории снимков...");
            AddInfoMessage("Функция в разработке. Здесь будет отображаться список предыдущих снимков.");
        }

        private void AddInfoMessage(string message)
        {
            textBoxInfo.AppendText(message + Environment.NewLine);
            textBoxInfo.ScrollToCaret();
        }

        private void TrackBarOpacity_Scroll(object sender, EventArgs e)
        {
            labelOpacity.Text = $"{trackBarOpacity.Value}%";
            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            if (originalImage == null || maskImage == null) return;

            float maskOpacity = trackBarOpacity.Value / 100f;

            // Создаём результат с размерами оригинала
            Bitmap result = new Bitmap(originalImage.Width, originalImage.Height);

            using (Graphics g = Graphics.FromImage(result))
            {
                // 1. Рисуем исходное УЗИ
                g.DrawImage(originalImage, 0, 0, originalImage.Width, originalImage.Height);

                // 2. Поверх рисуем маску (только там, где она белая)
                for (int y = 0; y < maskImage.Height; y++)
                {
                    for (int x = 0; x < maskImage.Width; x++)
                    {
                        Color maskPixel = maskImage.GetPixel(x, y);

                        // Если пиксель маски белый (область опухоли)
                        if (maskPixel.R > 200 && maskPixel.G > 200 && maskPixel.B > 200)
                        {
                            // Вычисляем координаты в масштабе оригинала
                            int scaledX = x * originalImage.Width / maskImage.Width;
                            int scaledY = y * originalImage.Height / maskImage.Height;

                            // Координаты не должны выходить за границы
                            if (scaledX >= 0 && scaledX < originalImage.Width &&
                                scaledY >= 0 && scaledY < originalImage.Height)
                            {
                                // Получаем цвет пикселя из оригинала
                                Color originalColor = originalImage.GetPixel(scaledX, scaledY);

                                // Прозрачность маски (alpha от 0 до 255)
                                int alpha = (int)(maskOpacity * 255);

                                // Смешиваем БЕЛЫЙ цвет маски с оригинальным цветом
                                // Чем выше alpha, тем больше белого
                                int red = (alpha * 255 + (255 - alpha) * originalColor.R) / 255;
                                int green = (alpha * 255 + (255 - alpha) * originalColor.G) / 255;
                                int blue = (alpha * 255 + (255 - alpha) * originalColor.B) / 255;

                                Color blendedColor = Color.FromArgb(255, red, green, blue);
                                result.SetPixel(scaledX, scaledY, blendedColor);
                            }
                        }
                    }
                }
            }

            // Обновляем картинку
            var oldImage = pictureBoxResult.Image;
            pictureBoxResult.Image = result;
            oldImage?.Dispose();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            httpClient?.Dispose();
            originalImage?.Dispose();
            maskImage?.Dispose();
            if (pictureBoxResult.Image != null)
                pictureBoxResult.Image.Dispose();
            base.OnFormClosing(e);
        }
    }
}