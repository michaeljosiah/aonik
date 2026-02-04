using FluentStorage.Blobs;
using Aonik.Application.Options;

namespace Aonik.Application.Abstractions.Storage;

public interface IBlobStorageFactory
{
    IBlobStorage Create(ContentTypeOptions contentTypeOptions);
}
