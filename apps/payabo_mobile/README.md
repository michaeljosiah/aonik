# Payabo Mobile (Flutter)

Flutter mobile client for Payabo, built from the design source in `apps/website/mobile`.

## Phase 1 Status

- Foundation scaffold created
- App architecture folders created
- Core dependencies added
- Environment config with mock mode created

## Prerequisites

- Flutter SDK (stable)
- Dart SDK (bundled with Flutter)

## Install Dependencies

```bash
flutter pub get
```

## Run In Mock Mode

```bash
flutter run \
  --dart-define=APP_ENV=dev \
  --dart-define=USE_MOCKS=true \
  --dart-define=API_BASE_URL=https://api.dev.payabo.local \
  --dart-define=PAYABO_TENANT_ID=<tenant-guid>
```

## Run Against Aonik API (Login + Registration)

```bash
flutter run \
  --dart-define=APP_ENV=dev \
  --dart-define=USE_MOCKS=false \
  --dart-define=API_BASE_URL=https://localhost:5001 \
  --dart-define=PAYABO_TENANT_ID=<same value as apps/Payabo/.env VITE_PAYABO_TENANT_ID> \
  --dart-define=AUTH0_CLIENT_ID=Xw3xY2u7FhoLcdc1VjfS0J7Zz6o0jN3R
```

This is the default runtime behavior when `USE_MOCKS` is not supplied.

## Run In Staging Mode

```bash
flutter run \
  --dart-define=APP_ENV=staging \
  --dart-define=USE_MOCKS=true \
  --dart-define=API_BASE_URL=https://api.staging.payabo.app \
  --dart-define=PAYABO_TENANT_ID=<tenant-guid>
```

## Run In Production Mode

```bash
flutter run \
  --dart-define=APP_ENV=prod \
  --dart-define=USE_MOCKS=false \
  --dart-define=API_BASE_URL=https://api.payabo.app \
  --dart-define=PAYABO_TENANT_ID=<tenant-guid>
```

## Folder Structure

```text
lib/
  app/
    environment/
    router/
  features/
  data/
  mock/
  shared/
```

## Notes

- App launch performs a basic API startup check against `/health` before proceeding from splash.
- `PAYABO_TENANT_ID` is required when `USE_MOCKS=false`.
- `AUTH0_CLIENT_ID` defaults to `Xw3xY2u7FhoLcdc1VjfS0J7Zz6o0jN3R` and can be overridden via dart-define.
- Android emulator automatically maps `localhost` API host to `10.0.2.2` for local development.
- Auth calls use `/auth/token`, `/v1/registrations/individual`, `/identity/password/forgot`, and `/identity/userinfo`.
- Profile calls use `/profiles/customers/me`, `/profiles/customers/me/email`, and `/profiles/customers/me/password`.
- Dashboard, catalog, orders, and payments continue using mock repositories for now.
