interface PlaceholderPageProps {
  title: string;
  description?: string;
}

export const PlaceholderPage = ({ title, description }: PlaceholderPageProps) => {
  return (
    <main className="main-wrapper">
      <section className="section section-sm">
        <div className="container">
          <h2 className="mb-3">{title}</h2>
          {description ? <p className="mb-0">{description}</p> : null}
        </div>
      </section>
    </main>
  );
};
