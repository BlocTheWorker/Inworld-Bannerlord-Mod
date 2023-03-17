import {
  createRequire
} from "module";

const require = createRequire(
  import.meta.url);
const localList = require('../Resource/badword.json').words;
const baseList = require('badwords-list').array;


/** This is taken from badwords npm package and modified (fixed bugs etc) */
class Filter {

  /**
   * Filter constructor.
   * @constructor
   * @param {object} options - Filter instance options
   * @param {boolean} options.emptyList - Instantiate filter with no blacklist
   * @param {array} options.list - Instantiate filter with custom list
   * @param {string} options.placeHolder - Character used to replace profane words.
   * @param {string} options.regex - Regular expression used to sanitize words before comparing them to blacklist.
   * @param {string} options.replaceRegex - Regular expression used to replace profane words with placeHolder.
   * @param {string} options.splitRegex - Regular expression used to split a string into words.
   */
  constructor(options = {}) {
    Object.assign(this, {
      list: options.emptyList && [] || Array.prototype.concat.apply(localList, [baseList, options.list || []]),
      exclude: options.exclude || [],
      splitRegex: options.splitRegex || /\b/,
      placeHolder: options.placeHolder === undefined ? '*' : options.placeHolder,
      regex: options.regex || /[^a-zA-Z0-9|\$|\@]|\^/g,
      replaceRegex: options.replaceRegex || /\w/g
    })
  }

  /**
   * Determine if a string contains profane language.
   * @param {string} string - String to evaluate for profanity.
   */
  isProfane(string) {
    return this.list
      .filter((word) => {
        const wordExp = new RegExp(`\\b${word.replace(/(\W)/g, '\\$1')}\\b`, 'gi');
        return !this.exclude.includes(word.toLowerCase()) && wordExp.test(string);
      })
      .length > 0 || false;
  }

  /**
   * Replace a word with placeHolder characters;
   * @param {string} string - String to replace.
   */
  replaceWord(string) {
    return string
      .replace(this.regex, '')
      .replace(this.replaceRegex, this.placeHolder);
  }

  /**
   * Evaluate a string for profanity and return an edited version.
   * @param {string} string - Sentence to filter.
   */
  clean(string) {
    const profaneWords = this.list.filter((word) => {
      const wordExp = new RegExp(`\\b${word.replace(/(\W)/g, '\\$1')}\\b|${word.replace(/(\W)/g, '\\$1')}`, 'gi');
      return !this.exclude.includes(word.toLowerCase()) && wordExp.test(string.toLowerCase());
    });
    if (profaneWords.length > 0) {
      let cleanedString = string;
      profaneWords.forEach(profaneWord => {
        const regex = new RegExp(`\\b${profaneWord}\\b|${profaneWord}`, 'gi');
        cleanedString = cleanedString.replace(regex, this.placeHolder.repeat(4));
      });
      return cleanedString;
    }
    return string;
  }

  /**
   * Add word(s) to blacklist filter / remove words from whitelist filter
   * @param {...string} word - Word(s) to add to blacklist
   */
  addWords() {
    let words = Array.from(arguments);
    this.list.push(...words);
    words
      .map(word => word.toLowerCase())
      .forEach((word) => {
        if (this.exclude.includes(word)) {
          this.exclude.splice(this.exclude.indexOf(word), 1);
        }
      });
  }

  /**
   * Add words to whitelist filter
   * @param {...string} word - Word(s) to add to whitelist.
   */
  removeWords() {
    this.exclude.push(...Array.from(arguments).map(word => word.toLowerCase()));
  }
}

const customFilter = new Filter({
  placeHolder: ''
});

// ironmonger is also blocked. yes. I dont know why.
customFilter.addWords('ironmonger', 'monger', 'b00b', 'ass', 'fuck', 'orgy');
customFilter.removeWords('nob', 'noble', 'spac', 'spaces', "voice", 'workspaces');

/** This is a sanitizer that runs above filter for objects, arrays and strings, recursively */
export class Sanitizer {

  /**
   * @param {string} name
   * @returns {boolean}
   */
  IsReserved(name) {
    return (name == "commonKnowledges" || name == 'uuid' || name == 'defaultCharacterAssets' || name == 'voice')
  }

  /**
   * @param {array || object || string} input
   * @returns {any}
   */
  CleanInput(input) {
    try {
      if (input == null || input == undefined) return input;

      // Check if input is an object
      if (typeof input === 'object' && !Array.isArray(input)) {
        const cleanedObj = {};
        // Loop through object properties and clean any string values
        for (const prop in input) {
          if (!(this.IsReserved(prop)))
            cleanedObj[prop] = this.CleanInput(input[prop])
          else
            cleanedObj[prop] = input[prop];
        }
        return cleanedObj;
      }

      // Check if input is an array
      if (Array.isArray(input)) {
        const cleanedArr = [];
        // Loop through array items and clean any string values
        input.forEach(item => {
          if (typeof item === 'string') {
            cleanedArr.push(customFilter.clean(item));
          } else {
            cleanedArr.push(item);
          }
        });
        return cleanedArr;
      }

      // Check if input is a string
      if (typeof input === 'string') {
        if (input) {
          return customFilter.clean(input);
        }
      }
      // If input is not an object, array, or string, return input as is
      return input;
    } catch {
      return input;
    }
  }
}