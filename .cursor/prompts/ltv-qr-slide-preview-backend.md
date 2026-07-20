# Cursor prompt — LTV QR Slide PDF preview (Kingsight API)

You are working in the **kingsightapi** ASP.NET Core Web API project (C#).

## Goal

Ensure **LTV Validation** can show QR slide PDFs in the SPA preview panel. The SPA calls:

```
GET /api/CmhcUpload/qr-slides/preview?link={encoded qr_slide_link}
GET /api/CmhcUpload/qr-slides/preview?fileName={Baytree.pdf}
```

The endpoint streams `application/pdf` from OneLake **`Files/external_files/qr_slides`** (same storage as File Upload → QR Slides).

## Already implemented (verify / extend if needed)

- `QrSlideLinkParser.TryExtractFileName(link)` — resolves Fabric portal URL, path, or bare `.pdf` file name
- `ICmhcFileStorage.GetQrSlideAsync(fileName)` — Fabric OneLake + local dev storage
- `ICmhcUploadService.GetQrSlidePreviewAsync(link)`
- `CmhcUploadController.PreviewQrSlide` — sets `Content-Security-Policy: frame-ancestors` for SPA iframe/blob preview

## Data requirement (critical)

`loan_alias_relationship.qr_slide_link` must resolve to a **PDF file name** that exists under qr_slides storage.

| Stored value | Works? |
|--------------|--------|
| `Baytree Condo Portfolio.pdf` | Yes |
| `https://.../path/Baytree Condo Portfolio.pdf` | Yes (path ends with `.pdf`) |
| `https://app.fabric.microsoft.com/groups/.../items/...` (no `.pdf` in URL) | **No** — parser cannot find file |

### Fix data when links are Fabric portal URLs only

1. Upload the PDF via **Mortgage → File Upload → QR Slides** (stores under OneLake `external_files/qr_slides`).
2. Update `wh_gold1.subjective_input.loan_alias_relationship.qr_slide_link` to the **stored PDF file name** (or a URL whose path ends with `.pdf`).

Example SQL (adjust loan_code / file name):

```sql
UPDATE [subjective_input].[loan_alias_relationship]
SET [qr_slide_link] = N'Baytree Condo Portfolio.pdf'
WHERE [loan_code] = N'LC0173-2';
```

Optional: add a dedicated column `qr_slide_file_name` if portal URLs must be kept separately — wire it in `LtvValidationService` list SQL and `QrSlideLinkParser`.

## Testing

1. Restart API after deploy.
2. `GET /api/CmhcUpload/qr-slides/preview?fileName=YourFile.pdf` → `200` + PDF bytes.
3. LTV Validation SPA → click **QR Slide Link** → right panel shows PDF.

## Auth

Preview uses SPA `HttpClient` (MSAL interceptor) → blob URL in iframe. Endpoint uses global Entra auth (no `[AllowAnonymous]`).

## Do NOT

- Embed `app.fabric.microsoft.com` in iframe (CSP `frame-ancestors` blocks it).
- Change the SPA unless the API contract changes.
