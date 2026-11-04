import 'dart:convert';
import 'dart:io';
import 'package:arke_world/arke_world.dart';
import 'package:crypto/crypto.dart';

Future<void> main(List<String> args) async {
  var catalogues=0,pins=0;
  await for (final entry in Directory(args.single).list()) {
    if(entry is! Directory) continue;
    final source=File('${entry.path}/catalogue.json');
    if(!await source.exists()) continue;
    final json=await source.readAsString();
    final parsed=parseCatalogue(json);
    if(parsed.worlds.isEmpty) throw StateError('Expected a published world');
    Future<void> visit(Object? value) async {
      if(value is Map<String,dynamic>) {
        if(value['url'] case final String url) {
          final file=File('${entry.path}/${Uri.parse(url).pathSegments.last}');
          if(await file.length()!=value['byteLength'] || (await sha256.bind(file.openRead()).first).toString()!=value['sha256']) throw StateError('Public pin does not identify exact retained bytes');
          pins++;
        }
        for(final child in value.values) { await visit(child); }
      } else if(value is List) { for(final child in value) { await visit(child); } }
    }
    await visit(jsonDecode(json));catalogues++;
  }
  if(catalogues!=7) throw StateError('Expected world, listening, guide, music, story, comic and part catalogues; got $catalogues');
  stdout.writeln('Pinned Player accepted $catalogues complete .NET catalogues and $pins exact public pins ($readerQualificationPin).');
}
const readerQualificationPin='27df122566bcdef18126932c15a74112e18b8573';
