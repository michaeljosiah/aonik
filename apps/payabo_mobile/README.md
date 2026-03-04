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

## Run In Mock Mode (Default)

```bash
flutter run \
  --dart-define=APP_ENV=dev \
  --dart-define=USE_MOCKS=true \
  --dart-define=API_BASE_URL=https://api.dev.payabo.local
```

## Run In Staging Mode

```bash
flutter run \
  --dart-define=APP_ENV=staging \
  --dart-define=USE_MOCKS=true \
  --dart-define=API_BASE_URL=https://api.staging.payabo.app
```

## Run In Production Mode

```bash
flutter run \
  --dart-define=APP_ENV=prod \
  --dart-define=USE_MOCKS=false \
  --dart-define=API_BASE_URL=https://api.payabo.app
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

- During Phase 1, repositories resolve to mock implementations.
- Live repository implementations are introduced in the API integration phase.
