using ClosedXML.Excel;
using Floreria.API.DTOs;
using Floreria.API.Models;

namespace Floreria.API.Services;

public interface IExcelExportService
{
    byte[] ExportOrdersToExcel(IEnumerable<Order> orders);
    byte[] ExportAttendanceToExcel(IEnumerable<Attendance> attendances);
    byte[] ExportInventoryToExcel(IEnumerable<Material> materials, Dictionary<string, decimal> reservedMap);
    byte[] ExportFinancialReportToExcel(FinancialSummaryDto summary, IEnumerable<Expense> expenses, string fromDate, string toDate);
}

public class ExcelExportService : IExcelExportService
{
    private static readonly XLColor HeaderBgColor = XLColor.FromHtml("#2E4F28"); // Verde botánico
    private static readonly XLColor HeaderTextColor = XLColor.White;

    public byte[] ExportOrdersToExcel(IEnumerable<Order> orders)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Pedidos y Ventas");

        // Título
        ws.Cell("A1").Value = "FLORISTERÍA LA CARRETA - REPORTE DE PEDIDOS Y VENTAS";
        ws.Range("A1:K1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Font.FontColor = HeaderBgColor;

        ws.Cell("A2").Value = $"Generado el: {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cell("A2").Style.Font.Italic = true;

        // Cabeceras
        string[] headers =
        [
            "N° Pedido", "Origen", "Fecha Entrega", "Hora", "Cliente",
            "Teléfono", "Destinatario", "Estado", "Total (COP)",
            "Abonado (COP)", "Saldo (COP)"
        ];

        int row = 4;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBgColor;
            cell.Style.Font.FontColor = HeaderTextColor;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        row++;
        foreach (var o in orders)
        {
            decimal total = o.Items.Sum(i => i.Price * i.Quantity) - o.Discount + o.Shipping;
            decimal paid = o.Payments.Sum(p => p.Amount);
            decimal balance = Math.Max(0, total - paid);

            ws.Cell(row, 1).Value = o.Number;
            ws.Cell(row, 2).Value = o.IsDirectSale ? "Venta en Tienda" : o.DeliveryMethod;
            ws.Cell(row, 3).Value = o.DeliveryDate;
            ws.Cell(row, 4).Value = o.Time;
            ws.Cell(row, 5).Value = o.Customer;
            ws.Cell(row, 6).Value = o.Phone;
            ws.Cell(row, 7).Value = o.Recipient;
            ws.Cell(row, 8).Value = o.Status.ToUpper();
            
            ws.Cell(row, 9).Value = total;
            ws.Cell(row, 9).Style.NumberFormat.Format = "$ #,##0";

            ws.Cell(row, 10).Value = paid;
            ws.Cell(row, 10).Style.NumberFormat.Format = "$ #,##0";

            ws.Cell(row, 11).Value = balance;
            ws.Cell(row, 11).Style.NumberFormat.Format = "$ #,##0";

            row++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportAttendanceToExcel(IEnumerable<Attendance> attendances)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Pase de Lista");

        ws.Cell("A1").Value = "FLORISTERÍA LA CARRETA - CONTROL DE ASISTENCIA Y PASE DE LISTA";
        ws.Range("A1:G1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Font.FontColor = HeaderBgColor;

        ws.Cell("A2").Value = $"Generado el: {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cell("A2").Style.Font.Italic = true;

        string[] headers = ["Fecha", "Trabajador", "Rol / Cargo", "Hora Entrada", "Hora Salida", "Estado Asistencia", "Observaciones"];
        int row = 4;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBgColor;
            cell.Style.Font.FontColor = HeaderTextColor;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        row++;
        foreach (var a in attendances.OrderByDescending(x => x.Date).ThenBy(x => x.User?.Name))
        {
            ws.Cell(row, 1).Value = a.Date;
            ws.Cell(row, 2).Value = a.User?.Name ?? "N/A";
            ws.Cell(row, 3).Value = a.User?.Role?.Name ?? "Colaborador";
            ws.Cell(row, 4).Value = a.ClockIn ?? "--:--";
            ws.Cell(row, 5).Value = a.ClockOut ?? "--:--";
            ws.Cell(row, 6).Value = a.Status;
            ws.Cell(row, 7).Value = a.Notes ?? "";

            // Formato condicional de estado
            if (a.Status == "Presente")
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Green;
            else if (a.Status == "Retardo")
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.DarkOrange;
            else if (a.Status == "Falta")
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;

            row++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportInventoryToExcel(IEnumerable<Material> materials, Dictionary<string, decimal> reservedMap)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Inventario Floral");

        ws.Cell("A1").Value = "FLORISTERÍA LA CARRETA - INVENTARIO Y STOCK DE MATERIALES";
        ws.Range("A1:J1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Font.FontColor = HeaderBgColor;

        ws.Cell("A2").Value = $"Generado el: {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cell("A2").Style.Font.Italic = true;

        string[] headers =
        [
            "Código", "Material / Flor", "Categoría", "Unidad",
            "Stock Físico", "Reservado", "Disponible", "Mínimo Alerta",
            "Costo Unitario", "Valor Total en Stock", "Proveedor"
        ];

        int row = 4;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBgColor;
            cell.Style.Font.FontColor = HeaderTextColor;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        row++;
        foreach (var m in materials.OrderBy(x => x.Category).ThenBy(x => x.Name))
        {
            decimal res = reservedMap.TryGetValue(m.Id, out var rVal) ? rVal : 0;
            decimal avail = m.Stock - res;
            decimal totalVal = m.Stock * m.Cost;

            ws.Cell(row, 1).Value = m.Id;
            ws.Cell(row, 2).Value = m.Name;
            ws.Cell(row, 3).Value = m.Category;
            ws.Cell(row, 4).Value = m.Unit;
            ws.Cell(row, 5).Value = m.Stock;
            ws.Cell(row, 6).Value = res;
            ws.Cell(row, 7).Value = avail;
            ws.Cell(row, 8).Value = m.Minimum;

            ws.Cell(row, 9).Value = m.Cost;
            ws.Cell(row, 9).Style.NumberFormat.Format = "$ #,##0";

            ws.Cell(row, 10).Value = totalVal;
            ws.Cell(row, 10).Style.NumberFormat.Format = "$ #,##0";

            ws.Cell(row, 11).Value = m.Supplier;

            if (avail <= m.Minimum)
            {
                ws.Cell(row, 7).Style.Font.Bold = true;
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            }

            row++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportFinancialReportToExcel(FinancialSummaryDto summary, IEnumerable<Expense> expenses, string fromDate, string toDate)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Resumen Financiero");

        ws.Cell("A1").Value = "FLORISTERÍA LA CARRETA - ESTADO FINANCIERO Y GASTOS";
        ws.Range("A1:D1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Font.FontColor = HeaderBgColor;

        ws.Cell("A2").Value = $"Período: {fromDate} al {toDate} | Generado el: {DateTime.Now:yyyy-MM-dd HH:mm}";
        ws.Cell("A2").Style.Font.Italic = true;

        ws.Cell("A4").Value = "INDICADOR FINANCIERO";
        ws.Cell("B4").Value = "VALOR (COP)";
        ws.Cell("A4").Style.Font.Bold = true;
        ws.Cell("B4").Style.Font.Bold = true;
        ws.Range("A4:B4").Style.Fill.BackgroundColor = HeaderBgColor;
        ws.Range("A4:B4").Style.Font.FontColor = HeaderTextColor;

        (string Concept, decimal Value)[] kpis =
        [
            ("Ventas Entregadas (Ingresos)", summary.Revenue),
            ("Costo Directo de Materiales", summary.Cost),
            ("Gastos Operativos", summary.Expenses),
            ("Costo por Merma y Deterioro", summary.Waste),
            ("Utilidad Neta Estimada", summary.Profit),
            ("Efectivo / Recaudos Cobrados", summary.Collected)
        ];

        int r = 5;
        foreach (var (concept, val) in kpis)
        {
            ws.Cell(r, 1).Value = concept;
            ws.Cell(r, 2).Value = val;
            ws.Cell(r, 2).Style.NumberFormat.Format = "$ #,##0";
            if (concept.Contains("Utilidad"))
            {
                ws.Range(r, 1, r, 2).Style.Font.Bold = true;
                ws.Cell(r, 2).Style.Font.FontColor = val >= 0 ? XLColor.Green : XLColor.Red;
            }
            r++;
        }

        r += 2;
        ws.Cell(r, 1).Value = "DETALLE DE GASTOS EN EL PERÍODO";
        ws.Range(r, 1, r, 4).Merge();
        ws.Cell(r, 1).Style.Font.Bold = true;
        ws.Cell(r, 1).Style.Font.FontSize = 12;

        r++;
        string[] expHeaders = ["Fecha", "Categoría", "Descripción", "Monto (COP)", "Método"];
        for (int i = 0; i < expHeaders.Length; i++)
        {
            var cell = ws.Cell(r, i + 1);
            cell.Value = expHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBgColor;
            cell.Style.Font.FontColor = HeaderTextColor;
        }

        r++;
        foreach (var exp in expenses)
        {
            ws.Cell(r, 1).Value = exp.Date;
            ws.Cell(r, 2).Value = exp.Category;
            ws.Cell(r, 3).Value = exp.Description;
            ws.Cell(r, 4).Value = exp.Amount;
            ws.Cell(r, 4).Style.NumberFormat.Format = "$ #,##0";
            ws.Cell(r, 5).Value = exp.Method;
            r++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
