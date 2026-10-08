import type { RegulatedAllergen } from '@/types/commerce';

export const ALLERGEN_LABELS: Record<RegulatedAllergen, string> = {
  Celery: 'Celery',
  CerealsContainingGluten: 'Cereals containing gluten',
  Crustaceans: 'Crustaceans',
  Eggs: 'Eggs',
  Fish: 'Fish',
  Lupin: 'Lupin',
  Milk: 'Milk',
  Molluscs: 'Molluscs',
  Mustard: 'Mustard',
  Peanuts: 'Peanuts',
  Sesame: 'Sesame',
  Soybeans: 'Soybeans',
  SulphurDioxideAndSulphites: 'Sulphur dioxide and sulphites',
  TreeNuts: 'Tree nuts',
};

export const REGULATED_ALLERGENS = Object.keys(ALLERGEN_LABELS) as RegulatedAllergen[];

/** A missing review must never become a declaration that none are present. */
export function formatAllergens(allergens: RegulatedAllergen[] | null | undefined): string | null {
  if (allergens == null) return null;
  return allergens.length === 0
    ? 'None of the 14 regulated allergens declared'
    : allergens.map((allergen) => ALLERGEN_LABELS[allergen]).join(', ');
}
