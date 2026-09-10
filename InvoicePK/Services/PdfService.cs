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

                            if (!string.IsNullOrWhiteSpace(invoice.Client.Address))
                            {
                                c.Item().PaddingTop(2).Row(r =>
                                {
                                    r.AutoItem().Width(10).Height(10).Svg(IconSvgMapPin);
                                    r.RelativeItem().PaddingLeft(5).Text(invoice.Client.Address).FontColor("#555555").FontSize(9.5f);
                                });
                            }
                            if (!string.IsNullOrWhiteSpace(invoice.Client.Phone))
                            {
                                c.Item().PaddingTop(2).Row(r =>
                                {
                                    r.AutoItem().Width(10).Height(10).Svg(IconSvgPhone);
                                    r.RelativeItem().PaddingLeft(5).Text(invoice.Client.Phone).FontColor("#555555").FontSize(9.5f);
                                });
                            }
                            if (!string.IsNullOrWhiteSpace(invoice.Client.Email))
                            {
                                c.Item().PaddingTop(2).Row(r =>
                                {
                                    r.AutoItem().Width(10).Height(10).Svg(IconSvgMail);
                                    r.RelativeItem().PaddingLeft(5).Text(invoice.Client.Email).FontColor("#555555").FontSize(9.5f);
                                });
                            }
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
                        var isWideTable = headersList.Count > 5;
                        var cellPadV = isWideTable ? 4 : 5;
                        var cellPadH = isWideTable ? 3 : 5;
                        var fontSize = isWideTable ? 7f : 8f;

                        // Dynamic Column definitions
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(34); // Sr. # (wide enough so "SR. #" never wraps into multiple lines)
                            foreach (var h in headersList)
                            {
                                cols.RelativeColumn(h.Equals("Description", StringComparison.OrdinalIgnoreCase) ? 2.5f : 1.8f);
                            }
                            cols.RelativeColumn(1.8f); // Amount
                        });

                        // Header row
                        IContainer HeaderCell(IContainer c) =>
                            c.Background("#1a1a1a").PaddingVertical(cellPadV).PaddingHorizontal(cellPadH);

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).AlignCenter()
                                .Text("SR. #").FontColor("#ffffff").Bold().FontSize(fontSize);
                            foreach (var header in headersList)
                            {
                                h.Cell().Element(HeaderCell)
                                    .Text(header.ToUpper()).FontColor("#ffffff").Bold().FontSize(fontSize);
                            }
                            h.Cell().Element(HeaderCell).AlignRight()
                                .Text("AMOUNT").FontColor("#ffffff").Bold().FontSize(fontSize);
                        });

                        // Item rows
                        for (int i = 0; i < itemsList.Count; i++)
                        {
                            var item = itemsList[i];
                            var bg = i % 2 == 0 ? "#ffffff" : "#F9F9F9";

                            IContainer DataCell(IContainer c, string bgCol) =>
                                c.Background(bgCol).BorderBottom(0.5f).BorderColor("#EEEEEE").PaddingVertical(cellPadV).PaddingHorizontal(cellPadH);

                            // Sr. #
                            table.Cell().Element(c => DataCell(c, bg)).AlignCenter()
                                .Text((i + 1).ToString()).FontSize(fontSize).FontColor("#777777");

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
                                    .Text(textVal).FontSize(fontSize);
                            }

                            // Amount
                            table.Cell().Element(c => DataCell(c, bg)).AlignRight()
                                .Text($"{currencySymbol} {item.Quantity * item.UnitPrice:N0}").FontSize(fontSize).Bold();
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

    private const string IconSvgMapPin = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='#666666' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0'/><circle cx='12' cy='10' r='3'/></svg>";
    private const string IconSvgPhone  = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='#666666' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M13.832 16.568a1 1 0 0 0 1.213-.303l.355-.465A2 2 0 0 1 17 15h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2A18 18 0 0 1 2 4a2 2 0 0 1 2-2h3a2 2 0 0 1 2 2v3a2 2 0 0 1-.8 1.6l-.468.351a1 1 0 0 0-.292 1.233 14 14 0 0 0 6.392 6.384'/></svg>";
    private const string IconSvgMail   = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='#666666' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><rect width='20' height='16' x='2' y='4' rx='2'/><path d='m22 7-8.991 5.727a2 2 0 0 1-2.009 0L2 7'/></svg>";
    private const string IconSvgNTN    = "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='#666666' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M14 2v4a2 2 0 0 0 2 2h4'/><path d='M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7z'/><path d='M9 13h6'/><path d='M9 17h4'/></svg>";

    private static void BuildBusinessInfo(ColumnDescriptor c, User user)
    {
        c.Item().Text(user.BusinessName ?? user.FullName)
            .FontSize(20).Bold().FontColor("#1a1a1a");

        if (!string.IsNullOrWhiteSpace(user.Address))
        {
            c.Item().PaddingTop(2).Row(r =>
            {
                r.AutoItem().Width(10).Height(10).Svg(IconSvgMapPin);
                r.RelativeItem().PaddingLeft(5).Text(user.Address).FontColor("#555555").FontSize(9.5f);
            });
        }

        if (!string.IsNullOrWhiteSpace(user.Phone))
        {
            c.Item().PaddingTop(2).Row(r =>
            {
                r.AutoItem().Width(10).Height(10).Svg(IconSvgPhone);
                r.RelativeItem().PaddingLeft(5).Text(user.Phone).FontColor("#555555").FontSize(9.5f);
            });
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            c.Item().PaddingTop(2).Row(r =>
            {
                r.AutoItem().Width(10).Height(10).Svg(IconSvgMail);
                r.RelativeItem().PaddingLeft(5).Text(user.Email).FontColor("#555555").FontSize(9.5f);
            });
        }

        if (!string.IsNullOrWhiteSpace(user.NTN))
        {
            c.Item().PaddingTop(2).Row(r =>
            {
                r.AutoItem().Width(10).Height(10).Svg(IconSvgNTN);
                r.RelativeItem().PaddingLeft(5).Text($"NTN: {user.NTN}").FontColor("#555555").FontSize(9.5f);
            });
        }
    }
}
