import csv
import json
import pathlib
import urllib.request


REPO_ROOT = pathlib.Path(__file__).resolve().parents[1]
SEED_DIR = REPO_ROOT / "src" / "Aonik.Infrastructure" / "Persistence" / "Seed" / "Data"

COUNTRIES_SOURCE_URL = (
    "https://raw.githubusercontent.com/mluqmaan/world-countries-json/refs/heads/main/countries.json"
)

CURRENCIES_SOURCE_URL = (
    "https://raw.githubusercontent.com/datasets/currency-codes/master/data/codes-all.csv"
)


def fetch_text(url: str) -> str:
    with urllib.request.urlopen(url) as response:  # noqa: S310
        return response.read().decode("utf-8")


def generate_countries() -> list[dict]:
    raw = fetch_text(COUNTRIES_SOURCE_URL)
    countries = json.loads(raw)

    out: list[dict] = []
    for item in countries:
        out.append(
            {
                "isoAlpha2": item.get("isoAlpha2") or "",
                "isoAlpha3": item.get("isoAlpha3") or "",
                "isoNumeric": item.get("isoNumeric"),
                "name": item.get("name") or "",
            }
        )

    out = [c for c in out if len(c["isoAlpha2"]) == 2 and c["name"].strip()]
    out.sort(key=lambda x: x["name"].casefold())
    return out


def normalize_minor_unit(raw: str | None) -> int | None:
    if raw is None:
        return None
    raw = raw.strip()
    if raw == "" or raw == "-":
        return None
    try:
        return int(raw)
    except ValueError:
        return None


def generate_currencies(include_withdrawn: bool) -> list[dict]:
    raw = fetch_text(CURRENCIES_SOURCE_URL)
    reader = csv.DictReader(raw.splitlines())

    out: dict[str, dict] = {}

    for row in reader:
        code = (row.get("AlphabeticCode") or "").strip().upper()
        if not code:
            continue

        name = (row.get("Currency") or "").strip()
        numeric = (row.get("NumericCode") or "").strip()
        minor_unit = normalize_minor_unit(row.get("MinorUnit"))
        withdrawal = (row.get("WithdrawalDate") or "").strip()

        if withdrawal and not include_withdrawn:
            continue

        record = {
            "code": code,
            "name": name,
            "numericCode": numeric or None,
            "minorUnit": minor_unit,
            "withdrawalDate": withdrawal or None,
        }

        # prefer the non-withdrawn record when duplicates exist
        if code in out:
            existing = out[code]
            if existing.get("withdrawalDate") and not record.get("withdrawalDate"):
                out[code] = record
            continue

        out[code] = record

    items = list(out.values())
    items.sort(key=lambda x: x["code"])
    return items


def write_json(path: pathlib.Path, payload) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def main() -> int:
    SEED_DIR.mkdir(parents=True, exist_ok=True)

    countries = generate_countries()
    write_json(SEED_DIR / "countries.derived.world-countries-json.json", countries)

    currencies = generate_currencies(include_withdrawn=True)
    write_json(SEED_DIR / "currencies.iso4217.canonical.json", currencies)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
