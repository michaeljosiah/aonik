import 'dart:convert';
import 'dart:io';
import '../lib/verify.dart';

Future<void> main(List<String> args) async {
  Map<String, Object?> result;
  var reportLimit = 256 * 1024;
  try {
    if (args.length != 7 && args.length != 8)
      throw const VerificationRefused('invalid-worker-arguments');
    if (args.length == 8) {
      reportLimit = int.parse(args[7]);
      if (reportLimit != 256 * 1024 && reportLimit != 1024 * 1024)
        throw const VerificationRefused('invalid-worker-limits');
    }
    result = await verifyPackage(
      args[0],
      args[1],
      int.parse(args[2]),
      maxAssets: int.parse(args[3]),
      maxAssetBytes: int.parse(args[4]),
      maxDocumentBytes: int.parse(args[5]),
      scratchRoot: args[6],
    );
  } on VerificationRefused catch (error) {
    result = refused(error.code);
  } on Object {
    result = refused('package-refused');
  }
  var encoded = jsonEncode(result);
  if (utf8.encode(encoded).length + 1 > reportLimit)
    encoded = jsonEncode(refused('report-too-large'));
  stdout.writeln(encoded);
}
