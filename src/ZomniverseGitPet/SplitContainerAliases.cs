// All SplitContainer declarations inside the application intentionally resolve to
// SafeSplitContainer. This retrofits Guardian, File Review, and project-preparation
// layouts without duplicating lifecycle safety logic in each form.
global using SplitContainer = ZomniverseGitPet.SafeSplitContainer;
