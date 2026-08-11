// ------------------------------------------------------------------------------------------------------
// LightningChart® example code: Demo shows how to build custom GPU accelerated table with annotations.
//
// If you need any assistance, or notice error in this example code, please contact support@lightningchart.com. 
//
// Permission to use this code in your application comes with LightningChart® license. 
//
// https://lightningchart.com | support@lightningchart.com | sales@lightningchart.com
//
// © LightningChart Ltd 2009-2026. All rights reserved.  
// ------------------------------------------------------------------------------------------------------

using LightningChartLib.WPF.Charting;
using LightningChartLib.WPF.Charting.Annotations;
using LightningChartLib.WPF.Charting.Axes;
using LightningChartLib.WPF.Charting.SeriesXY;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shape = LightningChartLib.WPF.Charting.Shape;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;

namespace InteractiveExamples
{
    /// <summary>
    /// Interaction logic for ExampleAnnotationTable.xaml
    /// </summary>
    public partial class ExampleAnnotationTable : Window, IDisposable
    {
        private LightningChart? _pmChart;
        private LightningChart? _tempChart;
        private LightningChart? _monthlyPMChart;

        public class AirQualityPoint
        {
            public DateTime Time { get; set; }
            public double Value { get; set; }
            public string Pollutant { get; set; } = "";
        }

        private List<AirQualityPoint> LoadData(string filename)
        {
            var data = new List<AirQualityPoint>();

            foreach (var line in File.ReadLines(filename).Skip(1))
            {
                var fields = line.Split(',');

                if (fields.Length < 8)
                    continue;

                if (!double.TryParse(
                    fields[5],
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double value))
                    continue;


                DateTime date =
                    DateTime.Parse(
                        fields[2] + " " + fields[3]
                    );


                data.Add(new AirQualityPoint
                {
                    Time = date,
                    Value = value
                });
            }

            return data;
        }

        // For loading CO and O3 data from pollutant_data.csv
        private List<AirQualityPoint> LoadPollutantData(
            string filename,
            string pollutant)
        {
            var data = new List<AirQualityPoint>();

            foreach (var line in File.ReadLines(filename).Skip(1))
            {
                var fields = line.Split(',');

                if (fields.Length < 6)
                    continue;

                // fields[0] = Pollutant
                if (!string.Equals(
                    fields[0].Trim(),
                    pollutant,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // fields[2] = Date
                // fields[3] = Start time
                if (!DateTime.TryParse(
                    fields[2].Trim() + " " + fields[3].Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime date))
                {
                    continue;
                }

                // fields[5] = Concentration
                if (!double.TryParse(
                    fields[5].Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double value))
                {
                    continue;
                }

                data.Add(new AirQualityPoint
                {
                    Time = date,
                    Value = value,
                    Pollutant = pollutant
                });
            }

            return data;
        }

        // For getting monthly average of O3 and CO
        private SeriesPoint[] CreatePollutantMonthlyAverage(
            List<AirQualityPoint> data,
            AxisX axis)
        {
            DateTime startDate = new DateTime(1997, 1, 1);
            DateTime endDate = new DateTime(2025, 1, 1);

            return data
                .Where(x => x.Time >= startDate && x.Time < endDate)
                .GroupBy(x => new
                {
                    x.Time.Year,
                    x.Time.Month
                })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new SeriesPoint
                {
                    X = axis.DateTimeToAxisValue(
                        new DateTime(
                            g.Key.Year,
                            g.Key.Month,
                            1
                        )
                    ),
                    Y = g.Average(x => x.Value)
                })
                .ToArray();
        }

        // Monthly average from PM data
        private SeriesPoint[] CreateMonthlyAverage(
            List<AirQualityPoint> data,
            AxisX axis)
        {
            return data
                .GroupBy(x => new
                {
                    x.Time.Year,
                    x.Time.Month
                })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new SeriesPoint
                {
                    X = axis.DateTimeToAxisValue(
                        new DateTime(
                            g.Key.Year,
                            g.Key.Month,
                            1
                        )
                    ),

                    Y = g.Average(
                        x => x.Value
                    )
                })
                .ToArray();
        }

        // Yearly average for PM data
        private SeriesPoint[] CreateYearlyAverage(
            List<AirQualityPoint> data,
            AxisX axis)
        {
            return data
                .GroupBy(x => x.Time.Year)
                .OrderBy(g => g.Key)
                .Select(g => new SeriesPoint
                {
                    X = axis.DateTimeToAxisValue(
                        new DateTime(g.Key, 7, 1) // July 1st
                    ),

                    Y = g.Average(x => x.Value)
                })
                .ToArray();
        }

        // Loading the daily temperate data from the TORONTO CITY station
        private List<AirQualityPoint> LoadDailyTemperatureData(string filename)
        {
            var data = new List<AirQualityPoint>();

            foreach (var line in File.ReadLines(filename).Skip(1))
            {
                var fields = line.Split(',');

                if (fields.Length < 15)
                    continue;

                // fields[4] = Date/Time
                if (!DateTime.TryParseExact(
                    fields[4].Trim('"'),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime date))
                {
                    continue;
                }

                // fields[13] = Mean Temp (°C)
                if (!double.TryParse(
                    fields[13].Trim('"'),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double temperature))
                {
                    continue;
                }

                data.Add(new AirQualityPoint
                {
                    Time = date,
                    Value = temperature
                });
            }

            return data;
        }

        // Loading the monthly temperature data from TORONTO LESTER B. PEARSON INT'L A
        private List<AirQualityPoint> LoadMonthlyTemperatureData(string filename)
        {
            var data = new List<AirQualityPoint>();

            foreach (var line in File.ReadLines(filename).Skip(1))
            {
                var fields = line.Split(',');

                if (fields.Length < 13)
                    continue;

                // fields[4] = Date/Time
                if (!DateTime.TryParseExact(
                    fields[4].Trim('"'),
                    "yyyy-MM",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime date))
                {
                    continue;
                }

                // fields[11] = Mean Temp (°C)
                if (!double.TryParse(
                    fields[11].Trim('"'),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double temperature))
                {
                    continue;
                }

                data.Add(new AirQualityPoint
                {
                    Time = date,
                    Value = temperature
                });
            }

            return data;
        }

        // Calculating monthly averages from  the daily temperature data
        private List<AirQualityPoint> CreateMonthlyTemperatureAverages(
            List<AirQualityPoint> dailyData)
        {
            return dailyData
                .GroupBy(x => new
                {
                    x.Time.Year,
                    x.Time.Month
                })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new AirQualityPoint
                {
                    Time = new DateTime(
                        g.Key.Year,
                        g.Key.Month,
                        1
                    ),

                    Value = g.Average(x => x.Value)
                })
                .ToList();
        }

        private SeriesPoint[] CreateTemperatureSeries(
            List<AirQualityPoint> data,
            AxisX axis)
        {
            return data
                .OrderBy(x => x.Time)
                .Select(x => new SeriesPoint
                {
                    X = axis.DateTimeToAxisValue(x.Time),
                    Y = x.Value
                })
                .ToArray();
        }

        // Loads both temperature files and combines them into a 
        // single list
        private List<AirQualityPoint> LoadAllTemperatureData()
        {
            var historicalData = LoadMonthlyTemperatureData(
                "temp_data.csv"
            );

            var dailyData = LoadDailyTemperatureData(
                "temp_data_daily.csv"
            );

            var dailyMonthlyAverages =
                CreateMonthlyTemperatureAverages(dailyData);

            var combined = historicalData
                .Where(x => x.Time < new DateTime(2013, 7, 1))
                .Concat(dailyMonthlyAverages)
                .OrderBy(x => x.Time)
                .ToList();

            return combined;
        }

        // Each months average PM for the bar chart
        private double[] CreateAveragePM25ByMonth(
            List<AirQualityPoint> data)
        {
            return data
                .Where(x => x.Time.Year >= 1997 && x.Time.Year <= 2024)
                .GroupBy(x => x.Time.Month)
                .OrderBy(g => g.Key)
                .Select(g => g.Average(x => x.Value))
                .ToArray();
        }

        private void DataCursor_ChangeResultContent(
            object sender,
            ViewXYDataCursorResultTableFormatEventArgs e)
        {
            if (e.Series is not BarSeries series)
                return;

            e.ResultContent = $"{series.Title.Text}\n\nAverage PM Concentration: {e.Y:0.00}";
        }

        private int _dataRowCount = 0;

        public ExampleAnnotationTable()
        {
            InitializeComponent();
            CreateChart();
        }
        private void CreateChart()
        {

            /*=======================
                    PM Chart
            =========================*/

            _pmChart = new LightningChart();

            _pmChart.BeginUpdate();
            _pmChart.ViewXY.DataCursor.Visible = true;
            _pmChart.ViewXY.AxisLayout.AutoAdjustMargins = false;
            _pmChart.ChartBackground.Color = Colors.Black;
            _pmChart.ViewXY.GraphBackground.Color = Colors.White;
            _pmChart.ViewXY.GraphBackground.GradientColor = Colors.Orange;

            _pmChart.ChartName = "Toronto 1997-2024";

            AxisX xAxis = _pmChart.ViewXY.XAxes[0];
            xAxis.Title.Text = "Date";
            xAxis.ValueType = AxisValueType.DateTime;

            xAxis.DateOriginYear = 1997;
            xAxis.DateOriginMonth = 1;
            xAxis.DateOriginDay = 1;

            _pmChart.ViewXY.YAxes.DisposeAllAndClear();

            AxisY yAxis = new AxisY(_pmChart.ViewXY);
            yAxis.Title.Text = "Concentration (µg/m³)";
            yAxis.SetRange(-5, 35);

            _pmChart.ViewXY.YAxes.Add(yAxis);
            _pmChart.ViewXY.XAxes[0].ScrollMode = XAxisScrollMode.None;
            _pmChart.ViewXY.XAxes[0].AllowUserInteraction = false;
            _pmChart.ViewXY.YAxes[0].AllowUserInteraction = false;
            _pmChart.ViewXY.AxisLayout.YAxisAutoPlacement = YAxisAutoPlacement.LeftThenRight;

            xAxis.MajorGrid.Color = Colors.White;
            yAxis.MajorGrid.Color = Colors.White;

            var data = LoadData(
                "pm_data.csv"
            );

            // YEARLY SERIES
            PointLineSeries yearlySeries =
                new PointLineSeries(
                    _pmChart.ViewXY,
                    xAxis,
                    yAxis
                );

            yearlySeries.LineStyle.Width = 3;
            yearlySeries.LineStyle.Color = Colors.Green;
            yearlySeries.PointsVisible = true;
            yearlySeries.Title.Text = "Yearly Average";

            yearlySeries.Points = CreateYearlyAverage(
                data,
                xAxis
            );

            _pmChart.ViewXY.PointLineSeries.Add(yearlySeries);

            // MONTHLY SERIES
            PointLineSeries monthlySeries =
                new PointLineSeries(
                    _pmChart.ViewXY,
                    xAxis,
                    yAxis
                );

            monthlySeries.LineStyle.Width = 1;
            monthlySeries.LineStyle.Color = Colors.Blue;
            monthlySeries.PointsVisible = false;
            monthlySeries.Title.Text = "Monthly Average";

            monthlySeries.Points = CreateMonthlyAverage(data, xAxis);

            _pmChart.ViewXY.PointLineSeries.Add(monthlySeries);

            // CO AND O3 DATA

            var pollutantFile =
                "pollutant_data.csv";

            var coData = LoadPollutantData(
                pollutantFile,
                "CO"
            );

            var o3Data = LoadPollutantData(
                pollutantFile,
                "O3"
            );

            // CO MONTHLY SERIES

            PointLineSeries coSeries =
                new PointLineSeries(
                    _pmChart.ViewXY,
                    xAxis,
                    yAxis
                );

            coSeries.LineStyle.Width = 2;
            coSeries.LineStyle.Color = Colors.HotPink;
            coSeries.PointsVisible = false;

            coSeries.Title.Text = "CO Monthly Average";

            coSeries.Points =
                CreatePollutantMonthlyAverage(
                    coData,
                    xAxis
                );

            _pmChart.ViewXY.PointLineSeries.Add(coSeries);


            // O3 MONTHLY SERIES

            PointLineSeries o3Series =
                new PointLineSeries(
                    _pmChart.ViewXY,
                    xAxis,
                    yAxis
                );

            o3Series.LineStyle.Width = 2;
            o3Series.LineStyle.Color = Colors.Purple;
            o3Series.PointsVisible = false;

            o3Series.Title.Text = "O₃ Monthly Average";

            o3Series.Points =
                CreatePollutantMonthlyAverage(
                    o3Data,
                    xAxis
                );

            _pmChart.ViewXY.PointLineSeries.Add(o3Series);

            xAxis.SetRange(
                xAxis.DateTimeToAxisValue(new DateTime(1997, 1, 1)),
                xAxis.DateTimeToAxisValue(new DateTime(2024, 12, 31))
            );

            yearlySeries.Title.Text = "Yearly Average";
            monthlySeries.Title.Text = "Monthly Average";

            // Legend
            _pmChart.ViewXY.LegendBoxes[0].Visible = true;

            _dataRowCount = 5;

            _pmChart.EndUpdate();


            gridPM25.Children.Add(_pmChart);


            /*=======================
                Temperature Chart
            =========================*/
            _tempChart = new LightningChart();

            _tempChart.BeginUpdate();

            _tempChart.ViewXY.DataCursor.Visible = true;
            _tempChart.ViewXY.AxisLayout.AutoAdjustMargins = false;
            _tempChart.ChartBackground.Color = Colors.Black;
            _tempChart.ViewXY.GraphBackground.Color = Colors.Orange;
            _tempChart.ViewXY.GraphBackground.GradientColor = Color.FromRgb(3, 121, 201);

            AxisX tempXAxis = _tempChart.ViewXY.XAxes[0];

            tempXAxis.ValueType = AxisValueType.DateTime;
            tempXAxis.Title.Text = "Date";

            tempXAxis.DateOriginYear = 1997;
            tempXAxis.DateOriginMonth = 1;
            tempXAxis.DateOriginDay = 1;

            _tempChart.ViewXY.YAxes.DisposeAllAndClear();

            AxisY tempYAxis = new AxisY(_tempChart.ViewXY);

            tempYAxis.Title.Text = "Temperature (°C)";
            tempYAxis.SetRange(-40, 40);

            _tempChart.ViewXY.YAxes.Add(tempYAxis);

            tempXAxis.MajorGrid.Color = Colors.White;
            tempYAxis.MajorGrid.Color = Colors.White;

            PointLineSeries tempSeries =
                new PointLineSeries(
                    _tempChart.ViewXY,
                    tempXAxis,
                    tempYAxis
                );

            tempSeries.LineStyle.Color = Colors.Red;
            tempSeries.LineStyle.Width = 2;
            tempSeries.PointsVisible = false;
            tempSeries.Title.Text = "Mean Temperature";

            var temperatureData = LoadAllTemperatureData();

            tempSeries.Points = CreateTemperatureSeries(
                temperatureData,
                tempXAxis
            );

            _tempChart.ViewXY.PointLineSeries.Add(tempSeries);

            tempXAxis.SetRange(
                tempXAxis.DateTimeToAxisValue(new DateTime(1997, 1, 1)),
                tempXAxis.DateTimeToAxisValue(new DateTime(2024, 12, 31))
            );

            _tempChart.ViewXY.LegendBoxes[0].Visible = true;

            _tempChart.EndUpdate();

            gridTemperature.Children.Add(_tempChart);
            

            /*============================
                Monthly PM2.5 Bar Chart
            ==============================*/

            _monthlyPMChart = new LightningChart();

            _monthlyPMChart.BeginUpdate();

            _monthlyPMChart.ViewXY.DataCursor.Visible = true;
            _monthlyPMChart.ViewXY.AxisLayout.AutoAdjustMargins = false;
            _monthlyPMChart.ViewXY.DataCursor.ChangeResultContent += DataCursor_ChangeResultContent;
            _monthlyPMChart.ViewXY.DataCursor.Configuration.XAxisLabelVisible = false;
            _monthlyPMChart.ViewXY.DataCursor.Configuration.XAxisLineVisible = false;
            _monthlyPMChart.ChartBackground.Color = Colors.Black;
            _monthlyPMChart.ViewXY.GraphBackground.Color = Color.FromRgb(3, 121, 201);
            _monthlyPMChart.ViewXY.GraphBackground.GradientColor = Colors.Black;

            _monthlyPMChart.ChartName = "Average PM2.5 by Month";

            AxisX monthlyXAxis = _monthlyPMChart.ViewXY.XAxes[0];

            monthlyXAxis.Title.Text = "Month";

            AxisY monthlyYAxis = _monthlyPMChart.ViewXY.YAxes[0];

            monthlyYAxis.Title.Text = "Average PM2.5 Concentration (µg/m³)";

            monthlyYAxis.SetRange(0, 20);


            // Load PM2.5 data
            var pmData = LoadData(
                "pm_data.csv"
            );

            double[] monthlyAverages =
                CreateAveragePM25ByMonth(pmData);

            BarSeries monthlyBarSeries =
                new BarSeries(
                    _monthlyPMChart.ViewXY,
                    monthlyXAxis,
                    monthlyYAxis
                );

            monthlyBarSeries.Title.Text = "Average PM2.5 by Month";

            monthlyBarSeries.BarThickness = 80;

            for (int month = 1; month <= 12; month++)
            {
                double average = monthlyAverages[month - 1];

                monthlyBarSeries.AddValue(
                    month,
                    average,
                    new DateTime(2000, month, 1)
                        .ToString("MMM"),
                    false
                );
            }

            _monthlyPMChart.ViewXY.BarViewOptions.Grouping =
                BarsGrouping.ByLocation;

            _monthlyPMChart.ViewXY.PointLineSeries.Clear();

            _monthlyPMChart.ViewXY.BarSeries.Add(monthlyBarSeries);

            monthlyXAxis.SetRange(0.5, 12.5);
            monthlyXAxis.CustomTicks.Clear();

            string[] monthNames =
            {
                "Jan", "Feb", "Mar", "Apr",
                "May", "Jun", "Jul", "Aug",
                "Sep", "Oct", "Nov", "Dec"
            };

            for (int i = 0; i < 12; i++)
            {
                monthlyXAxis.CustomTicks.Add(
                    new CustomAxisTick(
                        monthlyXAxis,
                        i + 1,
                        monthNames[i],
                        10,
                        true,
                        Colors.Gray,
                        CustomTickStyle.Tick
                    )
                );
            }

            monthlyXAxis.CustomTicksEnabled = true;
            monthlyXAxis.AutoFormatLabels = false;
            monthlyXAxis.InvalidateCustomTicks();
            monthlyXAxis.MajorGrid.Color = Colors.White;


            // Legend
            _monthlyPMChart.ViewXY.LegendBoxes[0].Visible = true;


            _monthlyPMChart.EndUpdate();

            gridMonthlyPM25.Children.Add(_monthlyPMChart);
        }
        
        public void Dispose()
        {
            // Don't forget to clear chart grid child list.
            gridPM25.Children.Clear();
            gridTemperature.Children.Clear();

            if (_pmChart != null)
            {
                // Chart's Dispose method needs to be called when chart is 
                // no longer needed so that all unmanaged resources 
                // (DirectX etc.) are released.
                _pmChart.Dispose();
                _pmChart = null;
            }
        }
    }
}
