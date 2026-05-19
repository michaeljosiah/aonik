import { createOpenAPI } from 'fumadocs-openapi/server';
import { createAPIPage } from 'fumadocs-openapi/ui';
import { resolve } from 'node:path';

const specPath = resolve(process.cwd(), 'openapi/aonik-api.yaml');

export const openapi = createOpenAPI({
  input: [specPath],
});

export const APIPage = createAPIPage(openapi, {});
