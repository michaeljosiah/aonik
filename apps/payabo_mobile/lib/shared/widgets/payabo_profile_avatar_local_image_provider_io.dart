import 'dart:io';

import 'package:flutter/painting.dart';

ImageProvider<Object>? createPayaboLocalImageProviderImpl(String value) {
  final trimmed = value.trim();
  if (trimmed.isEmpty) {
    return null;
  }

  if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
    return null;
  }

  final uri = Uri.tryParse(trimmed);
  if (uri != null && uri.hasScheme && uri.scheme != 'file') {
    return null;
  }

  final filePath =
      uri != null && uri.scheme == 'file' ? uri.toFilePath() : trimmed;

  if (filePath.isEmpty) {
    return null;
  }

  return FileImage(File(filePath));
}
