import { defineConfig } from '@kubb/core';
import createSwagger from '@kubb/swagger';
import createSwaggerTS from '@kubb/swagger-ts';
import createSwaggerTanstackQuery from '@kubb/swagger-tanstack-query';
export default defineConfig({
    root: '.',
    input: {
        path: './swagger.json',
    },
    output: {
        path: './src/api/generated',
        clean: true,
    },
    plugins: [
        createSwagger({}),
        createSwaggerTS({
            output: {
                path: 'types',
            },
        }),
        createSwaggerTanstackQuery({
            output: {
                path: 'hooks',
            },
            framework: 'vue',
            client: {
                importPath: '@/api/client',
            },
        }),
    ],
});
