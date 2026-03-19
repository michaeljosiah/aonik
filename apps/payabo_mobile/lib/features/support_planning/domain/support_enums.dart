/// Enumerations for the support planning feature.
///
/// These complement the [SupportType] enum from setup_enums.dart
/// which captures *who* the user supports. These enums capture
/// *how* support is structured and tracked.
library;

/// How frequently a support commitment recurs.
enum SupportFrequency {
  weekly,
  biWeekly,
  monthly,
  quarterly,
  oneOff,
}

/// The current status of a support plan.
enum SupportPlanStatus {
  active,
  paused,
  completed,
}

/// Category of support being provided.
enum SupportCategory {
  livingExpenses,
  education,
  medical,
  housing,
  business,
  general,
}
