import "jquery";

declare module "jquery" {
  interface JQuery {
    select2: (options?: unknown) => JQuery;
    slick: (options?: unknown) => JQuery;
  }
}
