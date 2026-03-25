import { useState, useCallback, useEffect } from 'react';
import useEmblaCarousel from 'embla-carousel-react';
import { ChevronLeft, ChevronRight, ExternalLink } from 'lucide-react';
import { cn } from '@/lib/utils';

interface BannerSlide {
  src: string;
  alt: string;
  /** Optional CTA label — if set a button is rendered over the slide */
  ctaLabel?: string;
  /** Optional click handler for the CTA / slide */
  onClick?: () => void;
}

interface BannerCarouselProps {
  images?: BannerSlide[];
  className?: string;
}

const placeholderImages: BannerSlide[] = [
  { src: '', alt: 'Banner 1' },
  { src: '', alt: 'Banner 2' },
  { src: '', alt: 'Banner 3' },
];

export function BannerCarousel({ images = placeholderImages, className }: BannerCarouselProps) {
  const [emblaRef, emblaApi] = useEmblaCarousel({ loop: true });
  const [selectedIndex, setSelectedIndex] = useState(0);
  const autoplayDelayMs = 4000;

  const onSelect = useCallback(() => {
    if (!emblaApi) return;
    setSelectedIndex(emblaApi.selectedScrollSnap());
  }, [emblaApi]);

  useEffect(() => {
    if (!emblaApi) return;
    onSelect();
    emblaApi.on('select', onSelect);
    return () => {
      emblaApi.off('select', onSelect);
    };
  }, [emblaApi, onSelect]);

  useEffect(() => {
    if (!emblaApi || images.length <= 1) return;
    const intervalId = window.setInterval(() => {
      emblaApi.scrollNext();
    }, autoplayDelayMs);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [emblaApi, images.length]);

  const scrollTo = useCallback(
    (index: number) => {
      if (emblaApi) emblaApi.scrollTo(index);
    },
    [emblaApi],
  );

  const scrollPrev = useCallback(() => emblaApi?.scrollPrev(), [emblaApi]);
  const scrollNext = useCallback(() => emblaApi?.scrollNext(), [emblaApi]);

  return (
    <div
      className={cn(
        'group relative overflow-hidden rounded-[4px] bg-gradient-to-br from-[var(--color-brand-primary)] to-[var(--color-brand-primary-dark,#044448)]',
        className ?? 'h-[225px]'
      )}
    >
      <div ref={emblaRef} className="overflow-hidden h-full">
        <div className="flex h-full">
          {images.map((image, index) => (
            <div key={index} className="flex-[0_0_100%] min-w-0 h-full relative">
              {image.src ? (
                <>
                  <img
                    src={image.src}
                    alt={image.alt}
                    className="absolute inset-0 w-full h-full object-cover"
                  />
                  {image.alt === 'Banner placeholder' && (
                    <div className="absolute inset-0 flex items-center justify-center bg-black/10">
                      <span className="text-white text-[20px] font-semibold tracking-wide">
                        Banner placeholder
                      </span>
                    </div>
                  )}
                </>
              ) : (
                <div className="absolute inset-0 flex items-center justify-center">
                  {/* Placeholder landscape illustration */}
                  <div className="relative w-full h-full overflow-hidden">
                    {/* Sky gradient */}
                    <div className="absolute inset-0 bg-gradient-to-b from-[#0a9ba4] to-[#0a7c84]" />

                    {/* Mountains */}
                    <svg
                      viewBox="0 0 800 300"
                      className="absolute bottom-0 w-full"
                      preserveAspectRatio="xMidYMax slice"
                    >
                      {/* Back mountains */}
                      <path
                        d="M0,300 L0,180 Q100,120 200,160 Q300,100 400,140 Q500,80 600,130 Q700,100 800,150 L800,300 Z"
                        fill="#066e75"
                      />
                      {/* Front hills */}
                      <path
                        d="M0,300 L0,220 Q150,180 300,210 Q450,170 600,200 Q700,180 800,220 L800,300 Z"
                        fill="#055a60"
                      />
                      {/* Foreground */}
                      <path
                        d="M0,300 L0,260 Q200,240 400,255 Q600,235 800,250 L800,300 Z"
                        fill="#044448"
                      />
                    </svg>

                    {/* Sheep silhouettes */}
                    <div className="absolute bottom-16 left-[15%]">
                      <div className="w-8 h-5 bg-white rounded-full opacity-90" />
                      <div className="w-4 h-4 bg-white rounded-full -mt-2 ml-0.5 opacity-90" />
                    </div>
                    <div className="absolute bottom-20 left-[25%]">
                      <div className="w-6 h-4 bg-white rounded-full opacity-80" />
                      <div className="w-3 h-3 bg-white rounded-full -mt-1.5 ml-0.5 opacity-80" />
                    </div>
                    <div className="absolute bottom-14 right-[30%]">
                      <div className="w-7 h-4 bg-white rounded-full opacity-85" />
                      <div className="w-3.5 h-3.5 bg-white rounded-full -mt-2 ml-0.5 opacity-85" />
                    </div>
                  </div>

                  {/* Text overlay */}
                  <div className="absolute inset-0 flex items-center justify-center">
                    <span className="text-white/60 text-xl font-medium tracking-wide">
                      Banner placeholder
                    </span>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* Navigation arrows */}
      {images.length > 1 && (
        <>
          <button
            onClick={scrollPrev}
            className="absolute left-3 top-1/2 -translate-y-1/2 p-1 text-white/80 hover:text-white transition-colors"
            aria-label="Previous slide"
          >
            <ChevronLeft className="w-9 h-9" />
          </button>
          <button
            onClick={scrollNext}
            className="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-white/80 hover:text-white transition-colors"
            aria-label="Next slide"
          >
            <ChevronRight className="w-9 h-9" />
          </button>
        </>
      )}

      {/* CTA button (for current slide) */}
      {images[selectedIndex]?.ctaLabel && (
        <button
          onClick={images[selectedIndex].onClick}
          className="absolute right-16 top-1/2 -translate-y-1/2 flex items-center gap-1.5 bg-white text-[var(--color-brand-primary)] px-4 py-2 rounded-md text-sm font-medium hover:bg-white/90 transition-colors"
        >
          {images[selectedIndex].ctaLabel}
          <ExternalLink className="w-4 h-4" />
        </button>
      )}

      {/* Rectangular dot indicators (Centrali style: 22x4px rectangles) */}
      <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-1.5">
        {images.map((_, index) => (
          <button
            key={index}
            onClick={() => scrollTo(index)}
            className={cn(
              'h-1 rounded-[1px] transition-all duration-300',
              selectedIndex === index
                ? 'w-[22px] bg-white'
                : 'w-[22px] bg-[#727272]/30 hover:bg-[#727272]/50',
            )}
            aria-label={`Go to slide ${index + 1}`}
          />
        ))}
      </div>

      <div className="pointer-events-none absolute inset-0 hidden overflow-hidden group-hover:block">
        <div className="shine-effect" />
      </div>
    </div>
  );
}
