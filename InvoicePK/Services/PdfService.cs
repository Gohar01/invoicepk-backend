using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InvoicePK.Models;
using InvoicePK.Helpers;

namespace InvoicePK.Services;

public class PdfService
{
    public PdfService()
    {
        // Set QuestPDF license (free for open source / small projects)
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
    }

    public byte[] GenerateInvoicePdf(Invoice invoice, User user)
    {
        var currencySymbol = CurrencyHelper.GetSymbol(invoice.Currency);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Liberation Sans"));

                page.Content().Column(col =>
                {
                    // ── Header ────────────────────────────────
                    col.Item().Row(row =>
                    {
                        // Business info (left) — now includes logo if present
                        row.RelativeItem().Row(inner =>
                        {
                            var logoBytes = DecodeLogo(user.LogoUrl);
                            if (logoBytes != null)
                            {
                                inner.ConstantItem(50).Height(50).Image(logoBytes).FitArea();
                                inner.RelativeItem().PaddingLeft(10).Column(c => BuildBusinessInfo(c, user));
                            }
                            else
                            {
                                inner.RelativeItem().Column(c => BuildBusinessInfo(c, user));
                            }
                        });

                        // Invoice title (right)
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("INVOICE")
                                .FontSize(28).Bold().FontColor("#00C16A");
                            c.Item().Text($"#{invoice.InvoiceNumber}")
                                .FontSize(14).Bold().FontColor("#333333");
                        });
                    });

                    col.Item().PaddingVertical(15).LineHorizontal(1).LineColor("#E0E0E0");

                    // ── Bill To + Dates ───────────────────────
                    col.Item().Row(row =>
                    {
                        // Bill To (left)
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("BILL TO").FontSize(9).Bold()
                                .FontColor("#888888").LetterSpacing(0.1f);
                            c.Item().PaddingTop(4)
                                .Text(invoice.Client.Name).Bold().FontSize(12);
                            if (invoice.Client.Email != null)
                                c.Item().Text(invoice.Client.Email).FontColor("#555555");
                            if (invoice.Client.Phone != null)
                                c.Item().Text(invoice.Client.Phone).FontColor("#555555");
                            if (invoice.Client.Address != null)
                                c.Item().Text(invoice.Client.Address).FontColor("#555555");
                        });

                        // Dates (right)
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Issue Date:").FontColor("#888888");
                                r.RelativeItem().AlignRight()
                                    .Text(invoice.IssueDate.ToString("dd MMM yyyy")).Bold();
                            });
                            c.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Due Date:").FontColor("#888888");
                                r.RelativeItem().AlignRight()
                                    .Text(invoice.DueDate.ToString("dd MMM yyyy")).Bold();
                            });
                            c.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Status:").FontColor("#888888");
                                r.RelativeItem().AlignRight()
                                    .Text(invoice.Status).Bold()
                                    .FontColor(invoice.Status == "Paid" ? "#00C16A" :
                                               invoice.Status == "Overdue" ? "#E53E3E" : "#333333");
                            });
                        });
                    });

                    col.Item().PaddingVertical(15);

                    // ── Line Items Table: Dynamic Grid ─────────
                    var itemsList = invoice.Items.ToList();
                    var headersList = new List<string> { "Description" };
                    if (itemsList.Count > 0 && itemsList[0].Description.Contains("\n[COLS:"))
                    {
                        var parts = itemsList[0].Description.Split("\n[COLS:");
                        var colsStr = parts[1].Split("]\n[VALS:")[0];
                        headersList = colsStr.Split('|').ToList();
                    }
                    else if (itemsList.Count > 0 && itemsList.Any(x => x.Quantity != 1 || x.UnitPrice != x.SubTotal))
                    {
                        headersList = new List<string> { "Description", "Qty", "Unit Price" };
                    }

                    col.Item().Table(table =>
                    {
                        // Dynamic Column definitions
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30); // Sr. #
                            foreach (var h in headersList)
                            {
                                cols.RelativeColumn(h.Equals("Description", StringComparison.OrdinalIgnoreCase) ? 3 : 2);
                            }
                            cols.RelativeColumn(2); // Amount
                        });

                        // Header row
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background("#1a1a1a").Padding(6);

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).AlignCenter()
                                .Text("SR. #").FontColor("#ffffff").Bold().FontSize(8);
                            foreach (var header in headersList)
                            {
                                h.Cell().Element(HeaderCell)
                                    .Text(header.ToUpper()).FontColor("#ffffff").Bold().FontSize(8);
                            }
                            h.Cell().Element(HeaderCell).AlignRight()
                                .Text("AMOUNT").FontColor("#ffffff").Bold().FontSize(8);
                        });

                        // Item rows
                        for (int i = 0; i < itemsList.Count; i++)
                        {
                            var item = itemsList[i];
                            var bg = i % 2 == 0 ? "#ffffff" : "#F9F9F9";

                            static IContainer DataCell(IContainer c, string bg) =>
                                c.Background(bg).BorderBottom(0.5f).BorderColor("#EEEEEE").Padding(6);

                            // Sr. #
                            table.Cell().Element(c => DataCell(c, bg)).AlignCenter()
                                .Text((i + 1).ToString()).FontSize(8).FontColor("#777777");

                            // Dynamic Columns
                            var rowVals = new Dictionary<string, string>();
                            string mainDesc = item.Description;

                            if (item.Description.Contains("\n[COLS:"))
                            {
                                var parts = item.Description.Split("\n[COLS:");
                                mainDesc = parts[0];
                                var colsStr = parts[1].Split("]\n[VALS:")[0];
                                var valsStr = parts[1].Split("]\n[VALS:")[1].Replace("]", "");
                                var cList = colsStr.Split('|');
                                var vList = valsStr.Split(" | ");

                                for (int cIdx = 0; cIdx < cList.Length; cIdx++)
                                {
                                    var rawVal = cIdx < vList.Length ? vList[cIdx] : "";
                                    var pairVal = rawVal.Contains(": ") ? rawVal.Split(": ")[1] : rawVal;
                                    rowVals[cList[cIdx]] = pairVal == "-" ? "" : pairVal;
                                }
                            }
                            else
                            {
                                rowVals["Description"] = item.Description;
                                rowVals["Qty"] = item.Quantity.ToString("G29");
                                rowVals["Unit Price"] = $"{currencySymbol} {item.UnitPrice:N0}";
                            }

                            foreach (var header in headersList)
                            {
                                var textVal = header.Equals("Description", StringComparison.OrdinalIgnoreCase)
                                    ? mainDesc
                                    : (rowVals.ContainsKey(header) ? rowVals[header] : "");

                                table.Cell().Element(c => DataCell(c, bg))
                                    .Text(textVal).FontSize(8);
                            }

                            // Amount
                            table.Cell().Element(c => DataCell(c, bg)).AlignRight()
                                .Text($"{currencySymbol} {item.Quantity * item.UnitPrice:N0}").FontSize(8).Bold();
                        }
                    });

                    col.Item().PaddingVertical(10);

                    // ── Totals ────────────────────────────────
                    col.Item().AlignRight().Width(220).Column(totals =>
                    {
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal").FontColor("#555555");
                            r.RelativeItem().AlignRight()
                                .Text($"{currencySymbol} {invoice.SubTotal:N0}");
                        });

                        if (invoice.GSTPercent > 0)
                        {
                            totals.Item().PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem()
                                    .Text($"GST ({invoice.GSTPercent}%)").FontColor("#555555");
                                r.RelativeItem().AlignRight()
                                    .Text($"{currencySymbol} {invoice.GSTAmount:N0}");
                            });
                        }

                        totals.Item().PaddingTop(8)
                            .Background("#00C16A").Padding(10).Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL").Bold().FontColor("#ffffff");
                            r.RelativeItem().AlignRight()
                                .Text($"{currencySymbol} {invoice.TotalAmount:N0}").Bold().FontColor("#ffffff");
                        });
                    });

                    // ── Notes ─────────────────────────────────
                    if (!string.IsNullOrEmpty(invoice.Notes))
                    {
                        col.Item().PaddingTop(20).Column(n =>
                        {
                            n.Item().Text("Notes").Bold().FontColor("#888888").FontSize(9);
                            n.Item().PaddingTop(4).Text(invoice.Notes).FontColor("#555555");
                        });
                    }

                    // ── Footer ────────────────────────────────
                    col.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor("#E0E0E0");
                    col.Item().PaddingTop(8).AlignCenter()
                        .Text("Thank you for your business!")
                        .FontColor("#888888").FontSize(9).Italic();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[]? DecodeLogo(string? logoUrl)
    {
        if (string.IsNullOrEmpty(logoUrl) || !logoUrl.Contains(",")) return null;
        try
        {
            var base64Part = logoUrl.Split(',')[1]; // strip "data:image/png;base64,"
            return Convert.FromBase64String(base64Part);
        }
        catch
        {
            return null; // if corrupt/invalid, just skip rendering it
        }
    }

    private static void BuildBusinessInfo(ColumnDescriptor c, User user)
    {
        c.Item().Text(user.BusinessName ?? user.FullName)
            .FontSize(22).Bold().FontColor("#1a1a1a");
        if (user.Address != null)
            c.Item().Text(user.Address).FontColor("#555555");
        if (user.Phone != null)
            c.Item().Text(user.Phone).FontColor("#555555");
        c.Item().Text(user.Email).FontColor("#555555");
        if (user.NTN != null)
            c.Item().Text($"NTN: {user.NTN}").FontColor("#555555");
    }
}
