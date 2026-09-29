import { createContext } from 'react';

/** Only an explicit domain label override replaces a page's own heading. */
export const ScreenLabelContext = createContext<string | undefined>(undefined);
