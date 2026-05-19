import './global.css';
import type { ReactNode } from 'react';
import { Inter } from 'next/font/google';
import { RootProvider } from 'fumadocs-ui/provider/next';

const inter = Inter({
  subsets: ['latin'],
});

export const metadata = {
  title: {
    default: 'Aonik Docs',
    template: '%s | Aonik Docs',
  },
  description:
    'Run your own AI-native financial platform. Aonik is the self-hosted, open-core platform you configure, integrate, and operate end-to-end.',
};

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html
      lang="en"
      className={`${inter.className} dark`}
      data-theme="dark"
      suppressHydrationWarning
    >
      <body className="flex min-h-screen flex-col">
        <RootProvider
          theme={{
            defaultTheme: 'dark',
            enableSystem: false,
          }}
        >
          {children}
        </RootProvider>
      </body>
    </html>
  );
}
