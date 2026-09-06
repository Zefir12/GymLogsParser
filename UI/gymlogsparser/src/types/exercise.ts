export interface ExerciseDefinition {
  id: string;
  name: string;
  category: "Strength" | "Cardio";
  muscleGroup: string;
  barbell: boolean;
}
