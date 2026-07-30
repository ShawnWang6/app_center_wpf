using ClosedXML.Excel;
using CtrlCenter.AppRptModel;
using CtrlCenter.DataModel;
using DocumentFormat.OpenXml.Spreadsheet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CtrlCenter.Excel
{
    public static class ExcelRptGenerator
    {
        static class RptPlaceholder
        {
            public const string DepName = "dep_name";
            public const string SwitchNo = "switch_no";
            public const string LineName = "line_name";
            public const string SwitchModel = "switch_model";
            //"2026-01-16  12:53:05" {{lrt_ra}}	{{lrt_ra_r}}	
            //{{lrt_rb}}	{{lrt_rb_r}}	{{lrt_rc}}	{{lrt_rc_r}}
            //{{lrt_rth}}	{{lrt_c}}	{{lrt_c_r}}	{{lrt_cth}}
            public const string LrtOperDate = "lrt_oper_date";
            public const string LrtRa = "lrt_ra";
            public const string LrtRaOk = "lrt_ra_r";
            public const string LrtRb = "lrt_rb";
            public const string LrtRbOk = "lrt_rb_r";
            public const string LrtRc = "lrt_rc";
            public const string LrtRcOk = "lrt_rc_r";
            public const string LrtRth = "lrt_rth";
            public const string LrtC = "lrt_c";
            public const string LrtCOk = "lrt_c_r";
            public const string LrtCth = "lrt_cth";
            //{{wvt_time}}		{{wvt_dc}}		{{wvt_ac}}		{{wvt_result}}	
            public const string WvtOperDate = "wvt_oper_date";
            public const string WvtDc = "wvt_dc";
            public const string WvtAc = "wvt_ac";
            public const string WvtResul = "wvt_result";
        }
        /// <summary>
        /// 生成带分页的报表
        /// </summary>
        /// <param name="templatePath">模板文件路径</param>
        /// <param name="outputPath">输出文件路径</param>
        /// <param name="rpts">报表数据</param>
        /// <param name="sheetName">模板sheet名</param>
        public static  void GenerateReport(string templatePath, string outputPath,
                                   IDictionary<AppType, RptFile> rpts,
                                   string sheetName = "lrt_hvt_zkc_at"
                                   )
        {
            var replaces = new Dictionary<string, string>()
            {
                { RptPlaceholder.DepName, string.Empty },
                { RptPlaceholder.SwitchNo, string.Empty },
                { RptPlaceholder.LineName, string.Empty },
                { RptPlaceholder.SwitchModel, string.Empty },
                { RptPlaceholder.LrtOperDate, string.Empty },
                { RptPlaceholder.LrtRa, string.Empty },
                { RptPlaceholder.LrtRaOk, string.Empty },
                { RptPlaceholder.LrtRb, string.Empty },
                { RptPlaceholder.LrtRbOk, string.Empty },
                { RptPlaceholder.LrtRc, string.Empty },
                { RptPlaceholder.LrtRcOk, string.Empty },
                { RptPlaceholder.LrtRth, string.Empty },
                { RptPlaceholder.LrtC, string.Empty },
                { RptPlaceholder.LrtCOk, string.Empty },
                { RptPlaceholder.LrtCth, string.Empty },
                { RptPlaceholder.WvtOperDate, string.Empty },
                { RptPlaceholder.WvtDc, string.Empty },
                { RptPlaceholder.WvtAc, string.Empty },
                { RptPlaceholder.WvtResul, string.Empty },
            };
            var lrtRptModel = rpts.ContainsKey(AppType.LRT) ? Util.BuildExcelRptModel<LrtRptModel>(rpts[AppType.LRT]) : null;
            var hvcRptModel = rpts.ContainsKey(AppType.HVC) ? Util.BuildExcelRptModel<HvcRptModel>(rpts[AppType.HVC]) : null;
            var atRptModel = rpts.ContainsKey(AppType.ZKC) ? Util.BuildExcelRptModel<ZkcAtRptModel>(rpts[AppType.ZKC]) : null;
            
            //固定表头-1
            if (lrtRptModel != null)
            {
                replaces[RptPlaceholder.LrtOperDate] = lrtRptModel.TestTime;
                replaces[RptPlaceholder.LrtRa] = lrtRptModel.Ra;
                replaces[RptPlaceholder.LrtRaOk] = lrtRptModel.RaOk;
                replaces[RptPlaceholder.LrtRb] = lrtRptModel.Rb;
                replaces[RptPlaceholder.LrtRbOk] = lrtRptModel.RbOk;
                replaces[RptPlaceholder.LrtRc] = lrtRptModel.Rc;
                replaces[RptPlaceholder.LrtRcOk] = lrtRptModel.RcOk;
                replaces[RptPlaceholder.LrtRth] = lrtRptModel.RangeR;
                replaces[RptPlaceholder.LrtC] = lrtRptModel.I;
                replaces[RptPlaceholder.LrtCOk] = lrtRptModel.IOk;
                replaces[RptPlaceholder.LrtCth] = lrtRptModel.RangeI;
            }
            //固定表头-2
            if (hvcRptModel != null)
            {
                replaces[RptPlaceholder.WvtOperDate] = hvcRptModel.TestTime;
                replaces[RptPlaceholder.WvtDc] = hvcRptModel.Dc;
                replaces[RptPlaceholder.WvtAc] = hvcRptModel.Ac;
                replaces[RptPlaceholder.WvtResul] = hvcRptModel.Result;
            }
            //固定表头-3
            if (atRptModel != null)
            {
                replaces[RptPlaceholder.DepName] = atRptModel.RptCfg.DeptName;
                replaces[RptPlaceholder.SwitchNo] = atRptModel.RptCfg.SwitchNo;
                replaces[RptPlaceholder.LineName] = atRptModel.RptCfg.LineName;
                replaces[RptPlaceholder.SwitchModel] = atRptModel.RptCfg.SwitchModel;
            }            
            
            using (var workbook = new XLWorkbook(templatePath))
            {
                var sheet = workbook.Worksheet(sheetName);

                //报表头及固定行填充
                string titleRange = "A2:M7"; //TODO 根据实际模板调整范围
                var targetCells = sheet.Range(titleRange).Cells();
                foreach (var cell in targetCells)
                {
                    var text = cell.GetString();
                    if (string.IsNullOrEmpty(text)) continue;
                    bool replace = false;
                    if (replaces.TryGetValue(text, out string value1))
                    {
                        cell.Value = value1;
                        replace = true;
                    }
                    if (replace) continue;
                    var key = text;
                    if (text.Length >= 4 && text[0] == '{' && text[1] == '{' && text[text.Length - 1] == '}' && text[text.Length - 2] == '}')
                    {
                        key = text.Substring(2, text.Length - 4);
                    }
                    if (replaces.TryGetValue(key, out string value))
                    {
                        cell.Value = value;
                    }
                }

                if (atRptModel != null)
                {
                    //TODO 根据实际模板调整模板行号-C数据
                    const int templateTitleIndex = 9;
                    const int templateRowIndex = 10;
                    const int maxRowOfFirstPage = 10;
                    const int maxRowOfOtherPage = 20;

                    // 1. 获取模板行标题和模板行
                    IXLRow templRowTitle = sheet.Row(templateTitleIndex);
                    IXLRow templRow = sheet.Row(templateRowIndex);
                    // 2. 记录模板行的样式和格式（复制用）
                    //    ClosedXML 的 CopyTo 会自动复制样式，不需要额外操作
                    // 3. 当前要填充的行号从模板行开始
                    int sheetRow = templateRowIndex;
                    var allRows = atRptModel.GetTotalRows();
                    // 4. 填充数据
                    bool isFirstPage = true;
                    int currPageDataRowCount = 0;//当前页填充行数
                    foreach(var rowData in allRows)
                    {
                        // 1. 插入垂直分页符：在 "B" 列之后分页（第2列之后）
                        //sheet.ColumnPageBreaks.Add(2);

                        // 2. 插入水平分页符：在第10行之后分页
                        //sheet.RowPageBreaks.Add(10);

                        if (isFirstPage) 
                        {
                            // 如果是第一行数据，直接使用模板行填充（保留模板行）
                            if (sheetRow == templateRowIndex)
                            {
                                FillRowData(sheet, sheetRow, rowData);
                            }
                            else
                            {
                                // 从第二行开始，复制模板行到新行
                                templRow.CopyTo(sheet.Row(sheetRow));
                                FillRowData(sheet, sheetRow, rowData);
                            }                            
                            sheetRow++;
                            currPageDataRowCount++;
                            if (currPageDataRowCount >= maxRowOfFirstPage)
                            {
                                isFirstPage = false;
                                currPageDataRowCount = 0;
                            }
                        }
                        else
                        {
                            if (currPageDataRowCount == 0)
                            {
                                // 先插入分页符，并复制标题行到新页
                                sheet.PageSetup.AddHorizontalPageBreak(sheetRow - 1);
                                templRowTitle.CopyTo(sheet.Row(sheetRow));
                                //sheet行号更新
                                sheetRow++;
                            }

                            // 复制模板行到新行并填充数据
                            templRow.CopyTo(sheet.Row(sheetRow));
                            FillRowData(sheet, sheetRow, rowData);


                            //sheet行号更新
                            sheetRow++;
                            //数据行更新
                            currPageDataRowCount++;
                            //如果满了就换页                            
                            if (currPageDataRowCount >= maxRowOfOtherPage)
                            {
                                currPageDataRowCount = 0;
                            }
                        }                                                
                    }
                }
                // ---------- 6. 保存文件 ----------
                workbook.SaveAs(outputPath);
            }
        }
        /// <summary>
        /// 将 List<string> 数据填充到指定行的各列（从第1列开始）
        /// </summary>
        private static void FillRowData(IXLWorksheet sheet, int rowIndex, List<string> rowData)
        {
            for (int col = 1; col <= rowData.Count; col++)
            {
                sheet.Cell(rowIndex, col).Value = rowData[col - 1] ?? "";
            }
        }

        /// <summary>
        /// 将数据字典填充到指定行的各列
        /// </summary>
        private static void FillDataRow(IXLWorksheet sheet, int rowNumber, Dictionary<string, object> data)
        {
            // 假设数据字典的Key为列号（如 "A", "B", "C"）或列索引
            foreach (var kvp in data)
            {
                string columnLetter = kvp.Key;
                object value = kvp.Value;

                var cell = sheet.Cell($"{columnLetter}{rowNumber}");
                if (value is DateTime dt)
                    cell.Value = dt;
                else if (value is decimal d)
                    cell.Value = d;
                else
                    cell.Value = value?.ToString() ?? "";
            }
        }
        private static void FillDataRow(IXLWorksheet sheet, int rowNumber, List<string> data)
        {
            // 假设数据字典的Key为列号（如 "A", "B", "C"）或列索引
            int col = 1;
            foreach (var value in data)
            {
                sheet.Cell(rowNumber, col++).Value = value;
            }
        }
    }
}
