export type FriendProfile = {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  relationship?: string;
};

export type FriendMessageDraft = {
  message: string;
  skipped: boolean;
};

export const friendSelectionStorageKey = "payabo:friend-selection";
export const friendMessageStorageKey = "payabo:friend-message";

export const loadFriendSelection = (): FriendProfile | null => {
  const raw = sessionStorage.getItem(friendSelectionStorageKey);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as FriendProfile;
  } catch {
    return null;
  }
};

export const saveFriendSelection = (friend: FriendProfile) => {
  sessionStorage.setItem(friendSelectionStorageKey, JSON.stringify(friend));
};

export const loadFriendMessage = (): FriendMessageDraft | null => {
  const raw = sessionStorage.getItem(friendMessageStorageKey);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as FriendMessageDraft;
  } catch {
    return null;
  }
};

export const saveFriendMessage = (message: FriendMessageDraft) => {
  sessionStorage.setItem(friendMessageStorageKey, JSON.stringify(message));
};
