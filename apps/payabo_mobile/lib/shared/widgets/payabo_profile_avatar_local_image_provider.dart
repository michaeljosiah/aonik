import 'package:flutter/painting.dart';

import 'payabo_profile_avatar_local_image_provider_stub.dart'
    if (dart.library.io) 'payabo_profile_avatar_local_image_provider_io.dart';

ImageProvider<Object>? createPayaboLocalImageProvider(String value) {
  return createPayaboLocalImageProviderImpl(value);
}
