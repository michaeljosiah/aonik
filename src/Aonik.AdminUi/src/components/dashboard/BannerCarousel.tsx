import { useState, useCallback, useEffect } from 'react';
import useEmblaCarousel from 'embla-carousel-react';
import { cn } from '@/lib/utils';

interface BannerCarouselProps {
  images?: { src: string; alt: string }[];
}

const placeholderImages = [
  { src: '', alt: 'Banner 1' },
  { src: '', alt: 'Banner 2' },
  { src: '', alt: 'Banner 3' },
];

export function BannerCarousel({ images = placeholderImages }: BannerCarouselProps) {
  const [emblaRef, emblaApi] = useEmblaCarousel({ loop: true });
  const [selectedIndex, setSelectedIndex] = useState(0);

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

  const scrollTo = useCallback(
    (index: number) => {
      if (emblaApi) emblaApi.scrollTo(index);
    },
    [emblaApi]
  );

  return (
    <div className="relative h-full rounded-md overflow-hidden bg-gradient-to-br from-[#5A8F7B] to-[#3D6B59]">
      <div ref={emblaRef} className="overflow-hidden h-full">
        <div className="flex h-full">
          {images.map((image, index) => (
            <div key={index} className="flex-[0_0_100%] min-w-0 h-full relative">
              {image.src ? (
                <img
                  src={image.src}
                  alt={image.alt}
                  className="absolute inset-0 w-full h-full object-cover"
                />
              ) : (
                <div className="absolute inset-0 flex items-center justify-center">
                  {/* Placeholder landscape illustration */}
                  <div className="relative w-full h-full overflow-hidden">
                    {/* Sky gradient */}
                    <div className="absolute inset-0 bg-gradient-to-b from-[#7BA89C] to-[#5A8F7B]" />
                    
                    {/* Mountains */}
                    <svg
                      viewBox="0 0 800 300"
                      className="absolute bottom-0 w-full"
                      preserveAspectRatio="xMidYMax slice"
                    >
                      {/* Back mountains */}
                      <path
                        d="M0,300 L0,180 Q100,120 200,160 Q300,100 400,140 Q500,80 600,130 Q700,100 800,150 L800,300 Z"
                        fill="#4A7A68"
                      />
                      {/* Front hills */}
                      <path
                        d="M0,300 L0,220 Q150,180 300,210 Q450,170 600,200 Q700,180 800,220 L800,300 Z"
                        fill="#3D6B59"
                      />
                      {/* Foreground */}
                      <path
                        d="M0,300 L0,260 Q200,240 400,255 Q600,235 800,250 L800,300 Z"
                        fill="#2D5449"
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

      {/* Dots indicator */}
      <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-2">
        {images.map((_, index) => (
          <button
            key={index}
            onClick={() => scrollTo(index)}
            className={cn(
              'w-2 h-2 rounded-full transition-all duration-200',
              selectedIndex === index
                ? 'bg-white w-6'
                : 'bg-white/50 hover:bg-white/70'
            )}
            aria-label={`Go to slide ${index + 1}`}
          />
        ))}
      </div>
    </div>
  );
}
