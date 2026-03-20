import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../theme/payabo_color_resolver.dart';
import '../theme/payabo_spacing.dart';
import 'payabo_modal_sheet.dart';

/// Result returned by [showAttachmentPickerSheet].
class AttachmentPickerResult {
  const AttachmentPickerResult({
    required this.filePath,
    required this.fileName,
  });

  /// Absolute path on the device file system.
  final String filePath;

  /// Display name (e.g. `receipt.jpg`).
  final String fileName;
}

/// Shows a bottom sheet allowing the user to attach a file by taking a photo,
/// choosing from the gallery, or picking a document (PDF, image).
///
/// Returns an [AttachmentPickerResult] or `null` if cancelled.
Future<AttachmentPickerResult?> showAttachmentPickerSheet({
  required BuildContext context,
}) async {
  return showPayaboModalSheet<AttachmentPickerResult>(
    context: context,
    title: 'Attach file',
    child: const _AttachmentPickerBody(),
  );
}

class _AttachmentPickerBody extends StatelessWidget {
  const _AttachmentPickerBody();

  @override
  Widget build(BuildContext context) {
    final c = context.colors;
    final textTheme = Theme.of(context).textTheme;

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        const SizedBox(height: PayaboSpacing.sm),

        // ── Take photo ──────────────────────────
        ListTile(
          leading: Icon(Icons.camera_alt_outlined, color: c.accentBrown),
          title: Text(
            'Take photo',
            style: textTheme.bodyLarge?.copyWith(color: c.accentBrown),
          ),
          onTap: () => _pickFromCamera(context),
        ),

        // ── Choose from gallery ─────────────────
        ListTile(
          leading: Icon(Icons.photo_library_outlined, color: c.accentBrown),
          title: Text(
            'Choose from gallery',
            style: textTheme.bodyLarge?.copyWith(color: c.accentBrown),
          ),
          onTap: () => _pickFromGallery(context),
        ),

        // ── Choose file ─────────────────────────
        ListTile(
          leading: Icon(Icons.attach_file_outlined, color: c.accentBrown),
          title: Text(
            'Choose file',
            style: textTheme.bodyLarge?.copyWith(color: c.accentBrown),
          ),
          onTap: () => _pickFile(context),
        ),
      ],
    );
  }

  Future<void> _pickFromCamera(BuildContext context) async {
    final navigator = Navigator.of(context);
    final XFile? image = await ImagePicker().pickImage(
      source: ImageSource.camera,
      maxWidth: 1200,
    );

    if (image != null) {
      navigator.pop(
        AttachmentPickerResult(
          filePath: image.path,
          fileName: image.name,
        ),
      );
    }
  }

  Future<void> _pickFromGallery(BuildContext context) async {
    final navigator = Navigator.of(context);
    final XFile? image = await ImagePicker().pickImage(
      source: ImageSource.gallery,
      maxWidth: 1200,
    );

    if (image != null) {
      navigator.pop(
        AttachmentPickerResult(
          filePath: image.path,
          fileName: image.name,
        ),
      );
    }
  }

  Future<void> _pickFile(BuildContext context) async {
    final navigator = Navigator.of(context);
    final FilePickerResult? result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: <String>['jpg', 'jpeg', 'png', 'gif', 'webp', 'pdf'],
    );

    if (result != null && result.files.isNotEmpty) {
      final PlatformFile file = result.files.first;
      if (file.path != null) {
        navigator.pop(
          AttachmentPickerResult(
            filePath: file.path!,
            fileName: file.name,
          ),
        );
      }
    }
  }
}
