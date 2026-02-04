interface ProgressBarProps {
  currentStep: number;
}

const steps = ["SELECT BILLER", "ENTER DETAILS", "PAYMENT DETAILS", "CHECKOUT", "THANK YOU"];

export const ProgressBar = ({ currentStep }: ProgressBarProps) => {
  return (
    <ul className="progressbar d-none d-lg-block">
      {steps.map((step, index) => {
        const stepClass = index < currentStep ? "current" : index === currentStep ? "active" : "";
        return (
          <li key={step} className={stepClass}>
            {step}
          </li>
        );
      })}
    </ul>
  );
};
