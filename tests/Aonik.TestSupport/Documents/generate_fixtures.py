"""Generate synthetic OCR fixtures; no real person, business, account or invoice data."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4
from reportlab.lib.utils import ImageReader

fixture_dir = Path(__file__).parent / "Fixtures"
fixture_dir.mkdir(exist_ok=True)
font_path = Path("C:/Windows/Fonts/arial.ttf")
if not font_path.exists():
    font_path = Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
font = ImageFont.truetype(str(font_path), 48)
sales = ["AONIK DEMO SALES INVOICE", "Customer: Demo Customer", "Invoice: DEMO-1001", "Currency: GBP", "Consulting: 2 x 125.00 = 250.00", "TOTAL: 250.00", "Due: 2026-12-01"]
supplier = ["SUPPLIER INVOICE", "Supplier: Example Supplier", "Reference: SUP-2001", "Currency: GBP", "Office supplies: 1 x 80.00 = 80.00", "TOTAL: 80.00", "Due: 2026-12-01"]

def raster(lines):
    image = Image.new("RGB", (1800, 1100), "white")
    draw = ImageDraw.Draw(image)
    for index, line in enumerate(lines):
        draw.text((100, 100 + index * 130), line, font=font, fill="black")
    return image

raster(sales).save(fixture_dir / "sales-invoice.png")
native = canvas.Canvas(str(fixture_dir / "sales-invoice-native.pdf"), pagesize=A4, invariant=1)
native.setFont("Helvetica", 18)
for index, line in enumerate(sales):
    native.drawString(42, 760 - index * 80, line)
native.showPage()
native.save()
sales_scanned = canvas.Canvas(str(fixture_dir / "sales-invoice-scanned.pdf"), pagesize=A4, invariant=1)
sales_scanned.drawImage(ImageReader(raster(sales)), 0, 240, width=A4[0], height=A4[0] * 1100 / 1800)
sales_scanned.showPage()
sales_scanned.save()
scanned = canvas.Canvas(str(fixture_dir / "supplier-invoice-scanned.pdf"), pagesize=A4, invariant=1)
scanned.drawImage(ImageReader(raster(supplier)), 0, 240, width=A4[0], height=A4[0] * 1100 / 1800)
scanned.showPage()
scanned.save()
(fixture_dir / "sales-invoice.txt").write_text("\n".join(sales) + "\n", encoding="utf-8")
